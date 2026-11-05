using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// A pending bank receipt and its allocations (M9.7 Phase 2.2). <see cref="ExpectedAmount"/> is what statement
/// reconciliation (Phase 5) may confirm: the deposited amount less every deposit since returned.
/// </summary>
public class ResponseBankReceiptJson
{
    public int FeeEvidenceVersion { get; init; }
    public IReadOnlyList<ResponseCardFeeEvidenceJson> FeeEvidenceHistory { get; init; } = [];
    public Guid? BankMovementId { get; init; }
    public Guid? BankTransactionId { get; init; }
    public Guid? SourceTransactionId { get; init; }
    public Guid? SourceAccountId { get; init; }
    public Guid? ReturnReceivableTransactionId { get; init; }
    public Guid? OriginalBankReceiptId { get; init; }
    public Guid? SourceSettlementEventId { get; init; }
    public Guid? CardBatchId { get; init; }
    public Guid? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? Brand { get; init; }
    public decimal? GrossAllocatedAmount { get; init; }
    public decimal? ConfirmedFee { get; init; }
    public bool FeeUnknown { get; init; }
    public string? FeeBasis { get; init; }
    public string? FeeEvidence { get; init; }
    public decimal? ObservedNetAmount { get; init; }
    public decimal? UnmatchedDifference { get; init; }
    public DateTime? PlannedArrivalOn { get; init; }
    public bool IsDebit { get; init; }
    public uint Version { get; init; }
    public Guid Id { get; init; }
    public Guid BankAccountId { get; init; }
    public string BankAccountName { get; init; } = string.Empty;
    public BankReceiptSourceKind SourceKind { get; init; }
    public BankReceiptStatus Status { get; init; }
    public DateTime OccurredOn { get; init; }
    public DateTime PostedOn { get; init; }
    public decimal Amount { get; init; }
    public decimal ReturnedAmount { get; init; }
    public decimal ExpectedAmount { get; init; }
    public string? Description { get; init; }
    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public IReadOnlyList<ResponseBankReceiptAllocationJson> Allocations { get; init; } = [];
}

/// <summary>One deposited cheque's claim on the receipt.</summary>
public class ResponseBankReceiptAllocationJson
{
    public Guid SettlementEventId { get; init; }
    public Guid TransactionId { get; init; }
    public decimal Amount { get; init; }

    /// <summary>The return event that took this amount back out of the receipt, if any.</summary>
    public Guid? ReturnedByEventId { get; init; }
}
