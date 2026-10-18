using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class RecoveryThrottle : EntityBase
{
    public string KeyHash { get; init; } = string.Empty;
    public DateTime NextAllowedAt { get; set; }
}
