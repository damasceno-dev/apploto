using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.Members;

public sealed record MemberAccountScope(
    Operator? LinkedOperator,
    IReadOnlyList<Guid> AllowedAccountIds);
