using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Members;

/// <summary>
/// The answer to "what can this Member see in this branch?".
/// Holds the user's operator (null if they have none) and the ids of
/// the accounts that operator is assigned to.
/// </summary>
public sealed record MemberAccountScope(
    Operator? LinkedOperator,
    IReadOnlyList<Guid> AllowedAccountIds);
