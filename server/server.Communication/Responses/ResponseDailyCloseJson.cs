using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseDailyCloseJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public Guid OpenedByUserId { get; init; }
    public string OpenedByUserName { get; init; } = string.Empty;
    public Guid? RecordedByUserId { get; init; }
    public string? RecordedByUserName { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public string? RecordedByOperatorName { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? SubmittedByUserName { get; init; }
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public string? RejectionReason { get; init; }
    public string? Notes { get; init; }
    public DateTime? ItemsFirstRecordedAt { get; init; }
    public DateTime? OpeningRecheckRequiredAt { get; init; }
    public Guid? OpeningRecheckTriggeredByDailyCloseId { get; init; }
    public Guid? OpeningRecheckTriggeredByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public IReadOnlyList<ResponseDailyCloseItemJson> Items { get; init; } = [];
}
