namespace server.Communication.Responses;

public class ResponseManagementOverviewHoursOperatorJson
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public decimal TotalHours { get; init; }
    public decimal TotalBalanceHours { get; init; }
    public bool ContainsInProgress { get; init; }
}
