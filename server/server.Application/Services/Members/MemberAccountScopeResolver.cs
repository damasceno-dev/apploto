using server.Domain.Interfaces;

namespace server.Application.Services.Members;

public sealed class MemberAccountScopeResolver(
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository)
    : IMemberAccountScopeResolver
{
    public async Task<MemberAccountScope> Resolve(Guid userId, Guid branchId)
    {
        var linkedOperator = await operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId);

        if (linkedOperator is null)
        {
            return new MemberAccountScope(null, []);
        }

        var linkedAccounts = await operatorAccountsRepository
            .ListActiveByOperatorIdAsNoTracking(linkedOperator.Id);

        return new MemberAccountScope(
            linkedOperator,
            linkedAccounts?.Select(operatorAccount => operatorAccount.AccountId).ToList() ?? []);
    }
}
