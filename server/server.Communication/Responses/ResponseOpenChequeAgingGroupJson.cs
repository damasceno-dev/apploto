using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One open-cheque group: an installment plan, or (M11 Phase 4.2) a standalone row of a type classified <c>Cheque</c>.
/// </summary>
public class ResponseOpenChequeAgingGroupJson
{
    /// <summary>The plan's origin transaction id; <c>null</c> for a standalone cheque, whose single row carries its id.</summary>
    public Guid? OriginTransactionId { get; init; }
    public decimal OutstandingTotal { get; init; }
    public DateTime OldestOpenDueDate { get; init; }
    public AgingBucket OldestOpenBucket { get; init; }
    public int OpenRowCount { get; init; }
    public int TotalRowCount { get; init; }
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<ResponseOpenChequeAgingRowJson> Rows { get; init; } = [];
}
