using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOpeningDiscrepancyAdjustmentJson
{
    public Guid Id { get; init; }
    public Guid DiscrepancyId { get; init; }
    public int ComparisonRevision { get; init; }
    public int Sequence { get; init; }
    public Guid? PreviousAdjustmentId { get; init; }
    public Guid? PriorAttributionId { get; init; }
    public Guid SessionId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public OpeningDiscrepancyAttribution Target { get; init; }
    public Guid? PreviousOperatorId { get; init; }
    public decimal PreviousSignedAmount { get; init; }
    public Guid? OperatorId { get; init; }
    public decimal SignedAmount { get; init; }
    public Guid ActorUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}
