namespace server.Domain.Entities;

public class SessionOpeningDiscrepancyRevisionItem : EntityBase
{
    public Guid RevisionId { get; init; }
    public SessionOpeningDiscrepancyRevision Revision { get; init; } = null!;

    public Guid ProductId { get; init; }
    public decimal ProposedValue { get; init; }
    public decimal AcknowledgedValue { get; init; }
    public decimal Delta { get; init; }
}
