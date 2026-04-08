using server.Domain.Entities.Enums;

namespace server.Domain.Models;

public class TokenResultValidation
{
    public bool IsSuccess { get; init; }
    public Guid UserId { get; init; }
    public TokenErrorType Error { get; init; }
}