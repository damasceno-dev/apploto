using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Immutable settlement evidence, gross allocations are the receipt's settlement events.</summary>
public class CardSettlementBatch : EntityBase
{
    public Guid BranchId { get; init; }
    public Guid ProviderId { get; init; }
    public CardProvider Provider { get; init; } = null!;
    public string ExternalIdentity { get; init; } = string.Empty;
    public Guid BankReceiptId { get; init; }
    public BankReceipt BankReceipt { get; init; } = null!;
    public string ProviderName { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public DateTime PlannedArrivalOn { get; init; }
    public decimal GrossAmount { get; init; }
    public decimal? ConfirmedFee { get; init; }
    public string? FeeBasis { get; init; }
    public string? FeeEvidence { get; init; }
    public bool ManagerConfirmedFee { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public FinancialClass FeeFinancialClass { get; init; } = FinancialClass.OperatingExpenseAndLoss;
    public Guid CreatedByUserId { get; init; }
}
