using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseDashboardCloseJson
{
    public Guid DailyCloseId { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? RecordedByUserId { get; init; }
    public string? RecordedByUserName { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public string? RecordedByOperatorName { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByUserName { get; init; }
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
    public DailyCloseStatus Status { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }

    /// <summary>Null when no submitted cash-variance row exists for this close.</summary>
    public decimal? VarianceValue { get; init; }
}
