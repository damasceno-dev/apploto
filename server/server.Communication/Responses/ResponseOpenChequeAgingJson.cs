using server.Domain.Entities.Enums;

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
    /// The cutoff's basis (M9.7 Phase 1.2): rows dated after <see cref="AsOfDate"/> never count, and an event counts
    /// when its PostedOn (financial basis, the default) or OccurredOn (operational basis) is on or before it.
    /// </summary>
    public ReceivableCutoffBasis CutoffBasis { get; init; }

    /// <summary>
    /// Whole-filter totals over every unpaid row of every open group matching the filters, independent of paging
    /// (M11 Phase 2.1). <see cref="TotalCount"/> is the open-group count: open plans plus standalone cheques
    /// (M11 Phase 4.2).
    /// </summary>
    public decimal OutstandingTotal { get; init; }
    public int OpenRowCount { get; init; }

    /// <summary>Every <c>AgingBucket</c> in declared order (zeros included), each open row bucketed at <see cref="AsOfDate"/>.</summary>
    public IReadOnlyList<ResponseAgingBucketTotalJson> ByBucket { get; init; } = [];
}
