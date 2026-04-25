using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Get;

public class GetTransactionUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberTransactionScopeResolver memberTransactionScopeResolver)
{
    public async Task<ResponseTransactionJson> Execute(Guid transactionId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is Role.Member)
        {
            return await ExecuteForMember(transactionId, branchUser.UserId, branchUser.BranchId);
        }

        var transaction = await transactionsRepository
            .GetByIdAndBranchIdAsNoTracking(transactionId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        return transaction.ToTransactionResponse();
    }

    private async Task<ResponseTransactionJson> ExecuteForMember(
        Guid transactionId,
        Guid userId,
        Guid branchId)
    {
        var memberScope = await memberTransactionScopeResolver.Resolve(userId, branchId);

        if (memberScope.LinkedOperator is null)
        {
            throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        var transaction = await transactionsRepository
            .GetByIdAndBranchIdAsNoTracking(transactionId, branchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_AVAILABLE_FOR_OPERATOR);

        if (memberScope.AllowedAccountIds.Contains(transaction.AccountId) is false)
        {
            throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_AVAILABLE_FOR_OPERATOR);
        }

        return transaction.ToTransactionResponse();
    }
}
