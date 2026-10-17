using server.Domain.Entities;
using server.Application.Services.Members;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Get;

public class GetTransactionUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver)
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

        return await ToResponseWithCounterpart(transaction, branchUser.BranchId);
    }

    /// <summary>
    /// A fiado source names its generated Terminal leg (M7.7 Phase 5.6/5.7). The id is only a navigation hint:
    /// reading that row still applies its own branch and account scope.
    /// </summary>
    private async Task<ResponseTransactionJson> ToResponseWithCounterpart(Transaction transaction, Guid branchId)
    {
        var counterpart = await transactionsRepository.GetGeneratedBySourceIdAndBranchIdAsNoTracking(transaction.Id, branchId);
        return transaction.ToTransactionResponse(counterpart?.Id);
    }

    private async Task<ResponseTransactionJson> ExecuteForMember(
        Guid transactionId,
        Guid userId,
        Guid branchId)
    {
        var memberScope = await memberAccountScopeResolver.Resolve(userId, branchId);

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

        return await ToResponseWithCounterpart(transaction, branchId);
    }
}
