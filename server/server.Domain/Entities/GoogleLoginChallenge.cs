namespace server.Domain.Entities;

public sealed class GoogleLoginChallenge : EntityBase
{
    public string StateHash { get; init; } = string.Empty;
    public string NonceHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? ConsumedAt { get; set; }
}
