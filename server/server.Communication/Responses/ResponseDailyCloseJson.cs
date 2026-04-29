using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseDailyCloseJson
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public DailyCloseStatus Status { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid BranchId { get; init; }
    public Guid? SubmittedByOperatorId { get; init; }
    public string? SubmittedByOperatorName { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public Guid? ApprovedByUserId { get; init; }
    public string? ApprovedByUserName { get; init; }
    public string? RejectionReason { get; init; }
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
    public IReadOnlyList<ResponseDailyCloseItemJson> Items { get; init; } = [];
}
