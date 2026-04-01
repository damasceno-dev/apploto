namespace server.Domain.Models;

public class TokenResultValidation
{
    public bool IsSuccess { get; init; }
    public Guid UserId { get; init; }
    public TokenErrorType Error { get; init; }
}

public enum TokenErrorType
{
    None,
    Expired,
    Invalid
}