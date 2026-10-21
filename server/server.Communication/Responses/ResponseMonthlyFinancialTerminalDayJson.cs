using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One Terminal/day: its close (when one exists) with recorder and submitter, its official variance (null without a
/// close or while Draft) and the day's bolão service fee. Never split between the operators who shared the Terminal.
/// </summary>
public class ResponseMonthlyFinancialTerminalDayJson
{
    public DateTime Date { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public bool AccountActive { get; init; }
    public Guid? DailyCloseId { get; init; }
    public DailyCloseStatus? CloseStatus { get; init; }
    public decimal? OfficialVariance { get; init; }

    /// <summary>The day's fee (<c>Σ In − Σ Out</c> of marked rows); <c>null</c> when the fee marker is not configured.</summary>
    public decimal? BolaoServiceFee { get; init; }
    public int BolaoServiceFeeRowCount { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public string? RecordedByUserName { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public string? RecordedByOperatorName { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByUserName { get; init; }
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
}
