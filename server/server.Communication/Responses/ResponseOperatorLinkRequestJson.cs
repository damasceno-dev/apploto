using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOperatorLinkRequestJson
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public OperatorLinkRequestStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public Guid? OperatorId { get; init; }
    public bool HasUsableAccount { get; init; }
}
