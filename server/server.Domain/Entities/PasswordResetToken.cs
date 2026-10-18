using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class PasswordResetToken : EntityBase
{
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public string TokenHash { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public DateTime? ConsumedAt { get; set; }
}
