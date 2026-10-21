using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 7: open cheque groups (installment plans and standalone cheques) as of today, with the whole-filter totals
/// of <c>GET /report/cheques/open-aging</c> and its first groups.
/// </summary>
public class ResponseManagementOverviewOpenChequesJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime AsOfDate { get; init; }

    /// <summary>Open groups: plans plus standalone cheques.</summary>
    public int TotalCount { get; init; }
    public int OpenRowCount { get; init; }
    public decimal OutstandingTotal { get; init; }
    public IReadOnlyList<ResponseAgingBucketTotalJson> ByBucket { get; init; } = [];
    public DateTime? OldestOpenDueDate { get; init; }
    public int PreviewLimit { get; init; }
    public IReadOnlyList<ResponseManagementOverviewOpenChequeJson> Items { get; init; } = [];
}
