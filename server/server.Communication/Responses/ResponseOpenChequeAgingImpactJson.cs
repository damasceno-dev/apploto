using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Open-cheque aging impact of a would-be installment plan: the single origin-grouped rollup the plan
/// would add to <c>GET /report/cheques/open-aging</c>. It mirrors the per-group shape of
/// <see cref="ResponseOpenChequeAgingGroupJson"/> (totals, oldest open due date/bucket, open/total row
/// counts, client/account identity, per-row breakdown) but is a distinct preview envelope: no
/// <c>OriginTransactionId</c> is exposed because no group exists yet, and the section reads "empty"
/// (<see cref="GroupAppearsInOpenCheques"/> false, zero totals, no rows) for a <c>Draft</c> plan, which
/// is invisible to the Active-filtered report. A fresh plan has no paid rows, so
/// <see cref="OpenRowCount"/> equals <see cref="TotalRowCount"/>.
/// </summary>
public class ResponseOpenChequeAgingImpactJson
{
    public bool GroupAppearsInOpenCheques { get; init; }
    public decimal OutstandingTotal { get; init; }
    public DateTime? OldestOpenDueDate { get; init; }
    public AgingBucket? OldestOpenBucket { get; init; }
    public int OpenRowCount { get; init; }
    public int TotalRowCount { get; init; }
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid? AccountId { get; init; }
    public string? AccountName { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<ResponseOpenChequeAgingPreviewRowJson> Rows { get; init; } = [];
}
