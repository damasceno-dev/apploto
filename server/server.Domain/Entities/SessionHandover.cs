using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class SessionHandover : EntityBase
{
    public uint Version { get; set; }

    public Guid BranchId { get; init; }
    public Guid AccountId { get; init; }

    public Guid OutgoingSessionId { get; init; }
    public DailyClose OutgoingSession { get; init; } = null!;

    public Guid? IncomingSessionId { get; set; }
    public DailyClose? IncomingSession { get; set; }

    public Guid IntendedIncomingOperatorId { get; init; }
    public Operator IntendedIncomingOperator { get; init; } = null!;

    public long OfferedCountRevision { get; init; }

    /// <summary>The confirmed count attempt the outgoing submission carried (null for offers made before Phase 4).</summary>
    public Guid? OfferedAttemptId { get; init; }
    public Guid OfferedByUserId { get; init; }
    public DateTime OfferedAt { get; init; }

    public SessionHandoverState State { get; set; } = SessionHandoverState.Offered;
    public Guid? ResolvedByUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Reason { get; set; }

    public ICollection<SessionHandoverItem> Items { get; init; } = [];
}
