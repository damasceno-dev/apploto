using server.Domain.Interfaces;

namespace server.Application.Services.Members;

/// <summary>
/// Runs two small read-only queries: first find the user's active
/// operator, then list that operator's active account links. Skips the
/// second query when there is no operator.
///
/// Does not cache. Call it once per request and reuse the result.
/// </summary>
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
