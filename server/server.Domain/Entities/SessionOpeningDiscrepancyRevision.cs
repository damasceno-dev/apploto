namespace server.Domain.Entities;

public class SessionOpeningDiscrepancyRevision : EntityBase
{
    public Guid DiscrepancyId { get; init; }
    public SessionOpeningDiscrepancy Discrepancy { get; init; } = null!;

    public int Revision { get; init; }
    public Guid? SourceId { get; init; }
    public long? SourceRevision { get; init; }
    public decimal ProposedTotal { get; init; }
    public decimal AcknowledgedTotal { get; init; }
    public decimal TotalDelta { get; init; }
    public Guid CreatedByUserId { get; init; }

    public ICollection<SessionOpeningDiscrepancyRevisionItem> Items { get; init; } = [];
}
