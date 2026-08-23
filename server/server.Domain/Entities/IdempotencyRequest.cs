namespace server.Domain.Entities;

public class IdempotencyRequest : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string ResponseEnvelope { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
}
