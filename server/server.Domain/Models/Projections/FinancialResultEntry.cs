using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>One economic effect, independent of mutable type names and bank confirmation.</summary>
public class FinancialResultEntry
{
    public Guid SourceId { get; init; }
    public FinancialResultSourceKind SourceKind { get; init; }
    public Guid? TransactionId { get; init; }
    public Guid? BankReceiptId { get; init; }
    public Guid? AccountId { get; init; }
    public string? AccountName { get; init; }
    public bool? AccountActive { get; init; }
    public Guid? OfficialAttemptId { get; init; }
    public bool SourceHistoryComplete { get; init; }
    public Guid? SessionId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public FinancialClass? SnapshotFinancialClass { get; init; }
    public bool ClassificationConflict { get; init; }
    public FinancialClass FinancialClass { get; init; }
    public ClassificationProvenance? Provenance { get; init; }
    public decimal? SignedAmount { get; init; }
}

public record FinancialClassTotal(
    FinancialClass FinancialClass,
    decimal KnownSignedTotal,
    decimal KnownVolume,
    int Count,
    int UnknownAmountCount,
    int MigratedCount);
public record FinancialResultPage(IReadOnlyList<FinancialClassTotal> Totals, int TotalCount, IReadOnlyList<FinancialResultEntry> Entries, int UnresolvedFeeCount, bool ClassificationComplete);
