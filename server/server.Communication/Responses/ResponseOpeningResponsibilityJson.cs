namespace server.Communication.Responses;

public class ResponseOpeningResponsibilityJson
{
    public ResponseOpeningDiscrepancyJson Discrepancy { get; init; } = new();
    public Guid? LatestAttributionId { get; init; }
    public DateTime SourceDate { get; init; }
    public bool SourceLocked { get; init; }
    public ResponseOpeningDiscrepancyAdjustmentJson? LatestAdjustment { get; init; }
    public List<ResponseOpeningDiscrepancyAdjustmentJson> Adjustments { get; init; } = [];
    public int? NextAfterSequence { get; init; }
}
