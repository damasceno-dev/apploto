using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseFinancialResultJson
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public DateTime AsOfDate { get; init; }
    public bool IsPartialMonth { get; init; }
    public decimal KnownOwnResult { get; init; }
    public bool ClassificationComplete { get; init; }
    public bool FeeAmountsComplete { get; init; }
    // M9.7 P3–P5 uses a subledger: matching does not prove a complete bank position.
    public bool BankPositionComplete { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public List<ResponseFinancialClassTotalJson> Classes { get; init; } = [];
    public List<ResponseFinancialResultEntryJson> Entries { get; init; } = [];
}

public class ResponseFinancialClassTotalJson
{
    public FinancialClass FinancialClass { get; init; }
    public decimal KnownSignedTotal { get; init; }
    public decimal KnownVolume { get; init; }
    public int Count { get; init; }
    public int UnknownAmountCount { get; init; }
    public int MigratedCount { get; init; }
}

public class ResponseFinancialResultEntryJson
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
