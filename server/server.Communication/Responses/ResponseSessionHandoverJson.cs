using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseSessionHandoverJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public Guid AccountId { get; init; }
    public Guid OutgoingSessionId { get; init; }
    public Guid? IncomingSessionId { get; init; }
    public Guid IntendedIncomingOperatorId { get; init; }
    public string IntendedIncomingOperatorName { get; init; } = string.Empty;
    public SessionHandoverState State { get; init; }
    public long OfferedCountRevision { get; init; }
    public Guid OfferedByUserId { get; init; }
    public DateTime OfferedAt { get; init; }
    public Guid? ResolvedByUserId { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public string? Reason { get; init; }
    public List<ResponseSessionHandoverItemJson> Items { get; init; } = [];
}
