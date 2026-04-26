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
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        // 404 stays reserved for missing/cross-branch ids so attackers can't probe
        // for transaction existence outside their branch.
        var transaction = await transactionsRepository
            .GetByIdAndBranchIdAsNoTracking(transactionId, branchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (memberScope.AllowedAccountIds.Contains(transaction.AccountId) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        }

        return transaction.ToTransactionResponse();
    }
}
