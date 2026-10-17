namespace server.Communication.Responses;

/// <summary>
/// A client's fiado statement (M7.7 Phase 5.5). <see cref="OutstandingTotal"/> and <see cref="OldestUnpaidDate"/>
/// are the branch-wide position as of <see cref="AsOfDate"/> (branch-local today); <see cref="OpeningBalance"/> and
/// <see cref="ClosingBalance"/> bound the whole window across every page. Balances are signed <c>Out − In</c>:
/// a negative value is client credit.
/// </summary>
public class ResponseClientFiadoStatementJson
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public DateTime AsOfDate { get; init; }
    public decimal OutstandingTotal { get; init; }
    public DateTime? OldestUnpaidDate { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public IReadOnlyList<ResponseClientFiadoStatementItemJson> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
}
