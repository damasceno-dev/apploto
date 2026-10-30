using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only custody audit of a session; review events stay in <see cref="DailyCloseReviewEvent"/>.</summary>
public class SessionEvent : EntityBase
{
    public Guid BranchId { get; init; }

    public Guid SessionId { get; init; }
    public DailyClose Session { get; init; } = null!;

    public SessionEventKind Kind { get; init; }
    public Guid ActorUserId { get; init; }
    public string ActorName { get; init; } = string.Empty;
    public string? Reason { get; init; }
    public Guid? RelatedId { get; init; }

    /// <summary>Server-serialized before/after snapshot; never bound from a request.</summary>
    public string Details { get; init; } = "{}";
}
