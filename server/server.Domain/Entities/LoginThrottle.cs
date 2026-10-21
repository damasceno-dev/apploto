namespace server.Domain.Entities;

/// <summary>
/// Persistent failed-password budget for one partition: a canonical account, or an account seen from one client network.
/// Holds only key hashes, counts and UTC instants — never an email, address or user id.
/// </summary>
public class LoginThrottle : EntityBase
{
    public string KeyHash { get; init; } = string.Empty;
    public string AccountKeyHash { get; init; } = string.Empty;
    public int FailureCount { get; set; }
    public DateTime WindowStartedAt { get; set; }
    public DateTime? BlockedUntil { get; set; }
    public DateTime ExpiresAt { get; set; }
}
