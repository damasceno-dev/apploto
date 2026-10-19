using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public sealed class ResponseOperatedOnboardingJson
{
    public Guid RequestId { get; init; }
    public Guid BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public Guid OwnerInvitationId { get; init; }
    public string OwnerEmail { get; init; } = string.Empty;
    public InvitationStatus OwnerInvitationStatus { get; init; }
    public MailDeliveryStatus DeliveryStatus { get; init; }
    public DateTime ExpiresAt { get; init; }
}
