using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class OpeningDiscrepancyAttributionEvent : EntityBase
{
    public Guid DiscrepancyId { get; init; }
    public SessionOpeningDiscrepancy Discrepancy { get; init; } = null!;

    public int DiscrepancyRevision { get; init; }
    public OpeningDiscrepancyAttribution Target { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
    public Guid? SupersedesEventId { get; init; }
}
