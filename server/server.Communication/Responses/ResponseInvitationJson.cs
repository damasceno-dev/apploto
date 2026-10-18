using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseInvitationJson
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public Role Role { get; init; }
    public Guid? OperatorId { get; init; }
    public InvitationStatus Status { get; init; }
    public DateTime ExpiresAt { get; init; }
    public MailDeliveryStatus DeliveryStatus { get; init; }
}
