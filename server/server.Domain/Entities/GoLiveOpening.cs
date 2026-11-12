namespace server.Domain.Entities;

/// <summary>Auditable opening identity. Its financial rows never represent a counter operation.</summary>
public class GoLiveOpening : EntityBase
{
    public DateTime? ClientCreditObservedAt { get; set; }
    public Guid? ClientCreditTransactionId { get; set; }
    public uint Version { get; set; }
    public Guid? PreviousId { get; init; }
    public string SourceReference { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public bool IsCheque { get; init; }
    public string Identity { get; init; } = string.Empty;
    public string SourceEvidence { get; init; } = string.Empty;
    public DateTime AsOfDate { get; init; }
    public Guid RecordedByUserId { get; init; }
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}
