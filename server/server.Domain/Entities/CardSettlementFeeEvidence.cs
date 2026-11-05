using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Append-only manager evidence. Never changes the original batch or its gross claim allocations.</summary>
public class CardSettlementFeeEvidence : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid BatchId { get; init; }
    public CardSettlementBatch Batch { get; init; } = null!;
    public int Sequence { get; init; }
    public decimal ConfirmedFee { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public string Basis { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime OccurredOn { get; init; }
    public DateTime PostedOn { get; init; }
    public FinancialClass FeeFinancialClass { get; init; } = FinancialClass.OperatingExpenseAndLoss;
    public Guid CreatedByUserId { get; init; }
}
