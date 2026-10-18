using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class OperatorLinkRequest : EntityBase
{
    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
    public Guid UserId { get; init; }
    public User User { get; init; } = null!;
    public OperatorLinkRequestStatus Status { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
