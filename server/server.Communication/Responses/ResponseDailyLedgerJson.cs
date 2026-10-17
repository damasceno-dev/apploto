namespace server.Communication.Responses;

public class ResponseDailyLedgerJson
{
    public IReadOnlyList<ResponseDailyLedgerItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal NetTotal { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;

    /// <summary>False when the drilled account is deactivated (M11 Phase 2.4: its history stays readable).</summary>
    public bool AccountActive { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
}
