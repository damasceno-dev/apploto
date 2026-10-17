namespace server.Communication.Responses;

public class ResponseFiadoAgingJson
{
    public IReadOnlyList<ResponseFiadoAgingItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public DateTime AsOfDate { get; init; }

    /// <summary>
    /// Whole-set totals over every open sale remainder matching the filters after the §6.4 FIFO netting, independent
    /// of paging (M11 Phase 3.2): the remainders' sum, one entry per <c>AgingBucket</c> (declared order, zeros
    /// included, bucketed by due date at <see cref="AsOfDate"/>), and the earliest sale <c>Date</c> (not due date)
    /// still holding a remainder — null when nothing is open.
    /// </summary>
    public decimal OutstandingTotal { get; init; }
    public IReadOnlyList<ResponseAgingBucketTotalJson> ByBucket { get; init; } = [];
    public DateTime? OldestUnpaidDate { get; init; }
}
