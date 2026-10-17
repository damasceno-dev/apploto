namespace server.Communication.Responses;

public class ResponseTimeEntryBalanceOperatorJson
{
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;

    /// <summary>False when a Manager/Admin drilled into a deactivated operator by id (M11 Phase 2.4).</summary>
    public bool OperatorActive { get; init; }
    public decimal TotalHours { get; init; }
    public decimal TotalBalanceHours { get; init; }
    public int PresentDays { get; init; }
    public int AbsentDays { get; init; }
    public int OwingDays { get; init; }
    public int AbonadoDays { get; init; }
    public bool ContainsInProgress { get; init; }
    public IReadOnlyList<ResponseTimeEntryBalanceSummaryDayJson> Days { get; init; } = [];
}
