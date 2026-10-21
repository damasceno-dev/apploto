namespace server.Communication.Responses;

public class ResponseManagementOverviewOperatorJson
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int TransactionCount { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }

    /// <summary>The automatic share, a subset of the totals above (never added to them).</summary>
    public int GeneratedCount { get; init; }
    public decimal GeneratedIn { get; init; }
    public decimal GeneratedOut { get; init; }
}
