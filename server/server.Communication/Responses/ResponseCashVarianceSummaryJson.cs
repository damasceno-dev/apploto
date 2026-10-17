namespace server.Communication.Responses;

public class ResponseCashVarianceSummaryJson
{
    public IReadOnlyList<ResponseCashVarianceSummaryItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public decimal TotalVariance { get; init; }
    public decimal MeanVariance { get; init; }
    public decimal MaxVariance { get; init; }
    public decimal MinVariance { get; init; }

    /// <summary>
    /// Official rows by close status over exactly the rows behind the totals (M11 Phase 2.2); they add up to
    /// <see cref="TotalCount"/>. Draft closes never contribute a row.
    /// </summary>
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
}
