using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One open cheque group; a standalone cheque has no plan id and is identified by its transaction id.</summary>
public class ResponseManagementOverviewOpenChequeJson
{
    public Guid? OriginTransactionId { get; init; }

    /// <summary>The group's oldest open row.</summary>
    public Guid OldestOpenTransactionId { get; init; }
    public decimal OutstandingTotal { get; init; }
    public DateTime OldestOpenDueDate { get; init; }
    public AgingBucket OldestOpenBucket { get; init; }
    public int OpenRowCount { get; init; }
    public int TotalRowCount { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
}
