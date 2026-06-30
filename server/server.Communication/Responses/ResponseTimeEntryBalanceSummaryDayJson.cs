using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseTimeEntryBalanceSummaryDayJson
{
    public DateTime Date { get; init; }
    public TimeEntryStatus Status { get; init; }
    public decimal TotalHours { get; init; }
    public decimal BalanceHours { get; init; }
    public bool IsInProgress { get; init; }
}
