using server.Domain.Interfaces;

namespace server.Application.Services.Transactions;

public sealed class MemberTransactionScopeResolver(
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository)
    : IMemberTransactionScopeResolver
{
    public async Task<MemberTransactionScope> Resolve(Guid userId, Guid branchId)
    {
        var linkedOperator = await operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId);

        if (linkedOperator is null)
        {
            return new MemberTransactionScope(null, []);
        }

        var linkedAccounts = await operatorAccountsRepository
            .ListActiveByOperatorIdAsNoTracking(linkedOperator.Id);

        return new MemberTransactionScope(
            linkedOperator,
            linkedAccounts?.Select(operatorAccount => operatorAccount.AccountId).ToList() ?? []);
    }
}
