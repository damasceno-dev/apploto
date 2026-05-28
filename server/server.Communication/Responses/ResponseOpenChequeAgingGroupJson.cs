using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOpenChequeAgingGroupJson
{
    public Guid OriginTransactionId { get; init; }
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
