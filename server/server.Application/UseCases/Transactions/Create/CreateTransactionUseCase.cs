using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Create;

public class CreateTransactionUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    ITransactionsRepository transactionsRepository,
    IUnitOfWork unitOfWork,
    TransactionBranchConsistencyService transactionBranchConsistencyService,
    LockDateGuard lockDateGuard,
    MemberAccountScopeGuard memberAccountScopeGuard)
{
    public async Task<ResponseCreateTransactionJson> Execute(RequestCreateTransactionJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var callerOperator = await operatorsRepository
            .GetActiveByUserIdAndBranchId(branchUser.UserId, branchUser.BranchId);

        var recordedByOperatorId = ResolveRecordedByOperatorId(
            request.RecordedByOperatorId,
            branchUser.Role,
            callerOperator);

        var transactionType = await transactionBranchConsistencyService.ResolveAndValidate(
            branchId: branchUser.BranchId,
            accountId: request.AccountId,
            recordedByOperatorId: recordedByOperatorId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId);

        await memberAccountScopeGuard.EnsureMemberCanActOnAccount(
            branchUser.Role,
            callerOperator?.Id,
            request.AccountId);

        if (transactionType.SettlementRule == SettlementRule.OperatorEnteredCheque &&
            request.DueDate is null)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE]);
        }

        var dueDate = DueDateCalculator.Compute(
            transactionType.SettlementRule,
            request.Date,
            request.DueDate);

        await lockDateGuard.EnsureNotLocked(branchUser.BranchId, request.Date);

        var transaction = request.ToTransaction(
            transactionType: transactionType,
            recordedByOperatorId: recordedByOperatorId,
            dueDate: dueDate,
            createdByUserId: branchUser.UserId,
            branchId: branchUser.BranchId);

        await transactionsRepository.Add(transaction);
        await unitOfWork.Commit();

        return transaction.ToCreateResponse();
    }

    /// <summary>
    /// Resolves the <c>RecordedByOperatorId</c> that the persisted row will carry. The
    /// spec (M3 §2.5) treats Member overrides as a shape-level DTO problem (400), not an
    /// authorization failure (403): the server always owns the value for Members, and the
    /// mere presence of a non-null override is a malformed request regardless of whether
    /// the value happens to match the caller's own operator. Same rule covers the
    /// Member-with-no-operator-link and Manager/Admin-with-no-link-and-no-override cases.
    /// </summary>
    private static Guid ResolveRecordedByOperatorId(Guid? requestedOperatorId, Role role, Operator? callerOperator)
    {
        if (role == Role.Member)
        {
            if (requestedOperatorId is not null)
            {
                throw new OnValidationException(
                    [ResourcesErrorMessages.TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR]);
            }

            if (callerOperator is null)
            {
                throw new OnValidationException(
                    [ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK]);
            }

            return callerOperator.Id;
        }

        if (requestedOperatorId is { } supplied)
        {
            return supplied;
        }

        return callerOperator?.Id
            ?? throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_REQUIRES_RECORDED_BY_OPERATOR]);
    }

    private static void Validate(RequestCreateTransactionJson request)
    {
        var result = new CreateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
