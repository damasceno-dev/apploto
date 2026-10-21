using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One operator/day from the shared time-entry calculation; <see cref="IsInProgress"/> marks an open segment.</summary>
public class ResponseMonthlyFinancialHoursDayJson
{
    public DateTime Date { get; init; }
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;
    public bool OperatorActive { get; init; }
    public TimeEntryStatus Status { get; init; }
    public decimal TotalHours { get; init; }
    public decimal BalanceHours { get; init; }
    public bool IsInProgress { get; init; }
}
