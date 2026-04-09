using server.Domain.Entities.Enums;

namespace server.Domain.Models;

public class TokenResultValidation
{
    public bool IsSuccess { get; init; }
    public Guid UserId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? BranchUserId { get; init; }
    public Role? Role { get; init; }
    public TokenScope Scope { get; init; } = TokenScope.Global;
    public TokenErrorType Error { get; init; }
}
