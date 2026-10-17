namespace server.Communication.Responses;

public class ResponseOpenChequeAgingJson
{
    public IReadOnlyList<ResponseOpenChequeAgingGroupJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public DateTime AsOfDate { get; init; }

    /// <summary>
    /// Whole-filter totals over every open installment row of every open plan matching the filters, independent of
    /// paging (M11 Phase 2.1). <see cref="TotalCount"/> stays the open-plan count.
    /// </summary>
    public decimal OutstandingTotal { get; init; }
    public int OpenRowCount { get; init; }

    /// <summary>Every <c>AgingBucket</c> in declared order (zeros included), each open row bucketed at <see cref="AsOfDate"/>.</summary>
    public IReadOnlyList<ResponseAgingBucketTotalJson> ByBucket { get; init; } = [];
}
