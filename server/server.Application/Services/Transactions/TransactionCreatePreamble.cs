using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public sealed record TransactionCreateContext(
    BranchUser BranchUser,
    Operator? CallerOperator,
    Guid RecordedByOperatorId,
    TransactionType TransactionType,
    DateTime? DueDate = null);

public class TransactionCreatePreamble(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    TransactionRecordedByOperatorResolver recordedByOperatorResolver,
    TransactionBranchConsistencyService transactionBranchConsistencyService,
    MemberAccountScopeGuard memberAccountScopeGuard,
    LockDateGuard lockDateGuard)
{
    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId,
            transactionDate: request.Date);

        if (context.TransactionType.SettlementRule == SettlementRule.OperatorEnteredCheque &&
            request.DueDate is null)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE]);
        }

        var dueDate = DueDateCalculator.Compute(
            context.TransactionType.SettlementRule,
            request.Date,
            request.DueDate);

        await lockDateGuard.EnsureNotLocked(context.BranchUser.BranchId, request.Date);

        return context with { DueDate = dueDate };
    }

    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionInstallmentJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId,
            transactionDate: request.Date);

        await lockDateGuard.EnsureNotLocked(context.BranchUser.BranchId, request.Date);

        return context;
    }

    private async Task<TransactionCreateContext> ResolveCommon(
        Guid? requestedRecordedByOperatorId,
        Guid accountId,
        Guid? clientId,
        Guid transactionTypeId,
        DateTime transactionDate)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var callerOperator = await operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId);

        var recordedByOperatorId = recordedByOperatorResolver.Resolve(
            requestedRecordedByOperatorId,
            branchUser.Role,
            callerOperator);

        var transactionType = await transactionBranchConsistencyService.ResolveAndValidate(
            branchId: branchUser.BranchId,
            accountId: accountId,
            recordedByOperatorId: recordedByOperatorId,
            clientId: clientId,
            transactionTypeId: transactionTypeId);

        await memberAccountScopeGuard.EnsureMemberCanActOnAccount(
            branchUser.Role,
            callerOperator?.Id,
            accountId);

        return new TransactionCreateContext(
            BranchUser: branchUser,
            CallerOperator: callerOperator,
            RecordedByOperatorId: recordedByOperatorId,
            TransactionType: transactionType);
    }
}
