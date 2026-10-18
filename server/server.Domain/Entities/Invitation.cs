using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class Invitation : EntityBase
{
    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;
    public string Email { get; init; } = string.Empty;
    public Role Role { get; init; }
    public Guid? OperatorId { get; set; }
    public Operator? Operator { get; init; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public InvitationStatus Status { get; set; }
    public Guid IssuedByUserId { get; init; }
    public Guid? AcceptedByUserId { get; set; }
    public DateTime? RespondedAt { get; set; }
    public MailDeliveryStatus DeliveryStatus { get; set; }
}
