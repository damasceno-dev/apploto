using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Transactions;

public sealed record MemberTransactionScope(
    Operator? LinkedOperator,
    IReadOnlyList<Guid> AllowedAccountIds);
