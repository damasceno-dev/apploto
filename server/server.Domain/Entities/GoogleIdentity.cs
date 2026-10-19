namespace server.Domain.Entities;

public sealed class GoogleIdentity : EntityBase
{
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public string Issuer { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
}
