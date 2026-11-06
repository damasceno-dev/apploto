using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only informational responsibility effect. Never a financial or payroll posting.</summary>
public class OpeningDiscrepancyAdjustment : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid DiscrepancyId { get; init; }
    public int ComparisonRevision { get; init; }
    public int Sequence { get; init; }
    public Guid? PreviousAdjustmentId { get; init; }
    public Guid? PriorAttributionId { get; init; }
    public Guid SessionId { get; init; }
    public Guid? PredecessorSessionId { get; init; }
    public Guid? HandoverId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public OpeningDiscrepancyAttribution Target { get; init; }
    public Guid? PreviousOperatorId { get; init; }
    public decimal PreviousSignedAmount { get; init; }
    public Guid? OperatorId { get; init; }
    public decimal SignedAmount { get; init; }
    public string Reason { get; init; } = string.Empty;
    public Guid ActorUserId { get; init; }
}
