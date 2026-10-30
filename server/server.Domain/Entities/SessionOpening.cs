using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// The immutable acknowledged opening of one session, with the proposal it was compared against. Its sum is the
/// session's TotalOpening in the §6.12 formula.
/// </summary>
public class SessionOpening : EntityBase
{
    public Guid BranchId { get; init; }

    public Guid SessionId { get; init; }
    public DailyClose Session { get; init; } = null!;

    public SessionOpeningEvidenceKind EvidenceKind { get; init; }
    public SessionOpeningSourceKind SourceKind { get; init; }
    public Guid? SourceId { get; init; }
    public long? SourceRevision { get; init; }
    public Guid? HandoverId { get; init; }

    public Guid AcknowledgedByUserId { get; init; }
    public Guid? AcknowledgedByOperatorId { get; init; }
    public DateTime AcknowledgedAt { get; init; }
    public string? Reason { get; init; }

    public ICollection<SessionOpeningItem> Items { get; init; } = [];
}
