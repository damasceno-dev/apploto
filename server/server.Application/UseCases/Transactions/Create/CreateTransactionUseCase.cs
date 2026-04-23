using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
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
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId);

        var recordedByOperatorId = TransactionBranchConsistencyService.RecordedByOperatorResolver(
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

    private static void Validate(RequestCreateTransactionJson request)
    {
        var result = new CreateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
