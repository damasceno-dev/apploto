using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseSessionEventJson
{
    public Guid Id { get; init; }
    public SessionEventKind Kind { get; init; }
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public Guid? RelatedId { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Server-serialized JSON snapshot of the change (before/after, ids, revisions).</summary>
    public string Details { get; init; } = "{}";
}
