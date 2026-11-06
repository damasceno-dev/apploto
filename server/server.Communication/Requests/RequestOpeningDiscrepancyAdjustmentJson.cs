using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestOpeningDiscrepancyAdjustmentJson
{
    public Guid DiscrepancyId { get; init; }
    public uint ExpectedVersion { get; init; }
    public int ComparisonRevision { get; init; }
    public int ExpectedAdjustmentSequence { get; init; }
    public Guid? PreviousAdjustmentId { get; init; }
    public Guid? PriorAttributionId { get; init; }
    public OpeningDiscrepancyAttribution Target { get; init; }
    public DateTime PostedOn { get; init; }
    public string Reason { get; init; } = string.Empty;
}
