using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseManagementOverviewCloseJson
{
    public Guid DailyCloseId { get; init; }
    public DateTime Date { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public bool AccountActive { get; init; }
    public DailyCloseStatus Status { get; init; }
    public decimal? OfficialVariance { get; init; }
    public Guid? RecordedByUserId { get; init; }
    public string? RecordedByUserName { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public string? RecordedByOperatorName { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByUserName { get; init; }
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
}
