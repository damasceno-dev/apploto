using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// A nonzero difference between a session's acknowledged opening and its proposal, keyed to the opening and not to
/// a handover. Revisions are append-only; the current attribution counts toward a person only while it names
/// <see cref="CurrentRevision"/>.
/// </summary>
public class SessionOpeningDiscrepancy : EntityBase
{
    public uint Version { get; set; }

    public Guid BranchId { get; init; }

    public Guid SessionOpeningId { get; init; }
    public SessionOpening SessionOpening { get; init; } = null!;

    public Guid SessionId { get; init; }
    public DailyClose Session { get; init; } = null!;

    public Guid? HandoverId { get; init; }
    public Guid? PredecessorSessionId { get; set; }
    public SessionOpeningSourceKind SourceKind { get; init; }

    public int CurrentRevision { get; set; }
    public decimal CurrentTotalDelta { get; set; }

    public OpeningDiscrepancyAttribution Attribution { get; set; } = OpeningDiscrepancyAttribution.Unattributed;
    public int? AttributedRevision { get; set; }

    public ICollection<SessionOpeningDiscrepancyRevision> Revisions { get; init; } = [];
}
