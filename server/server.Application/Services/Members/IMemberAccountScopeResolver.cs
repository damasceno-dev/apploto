namespace server.Application.Services.Members;

public interface IMemberAccountScopeResolver
{
    /// <summary>
    /// Finds out what a Member is allowed to see in a branch.
    ///
    /// The chain is: User → their Operator in this branch → the accounts
    /// that Operator works on. Every Member-facing feature (transactions,
    /// time entries, reports) calls this same method, so the visibility
    /// rule lives in one place only.
    /// </summary>
    /// <returns>
    /// Never null. There are two possible results:
    ///
    /// - The user has no operator in this branch:
    ///   LinkedOperator is null and AllowedAccountIds is empty.
    ///
    /// - The user has an operator:
    ///   LinkedOperator is that operator, and AllowedAccountIds holds the
    ///   ids of its accounts (can be empty if none are assigned).
    ///
    /// "No operator" and "operator with no accounts" look similar but are
    /// not the same — callers treat them differently.
    /// </returns>
    Task<MemberAccountScope> Resolve(Guid userId, Guid branchId, CancellationToken ct = default);
}
