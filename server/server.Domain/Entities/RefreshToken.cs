namespace server.Domain.Entities;

public class RefreshToken : EntityBase
{
    public Guid SessionId { get; init; } = Guid.NewGuid();
    public string Value { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
}
