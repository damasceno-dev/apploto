using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Review-specific view of a daily close. The properties from <see cref="Id"/> through
/// <see cref="UpdatedByUserId"/> intentionally mirror the header of <see cref="ResponseDailyCloseJson"/>
/// so the review screen receives the complete close context in one response.
///
/// <see cref="Items"/> is the review-specific part of this contract: unlike
/// <see cref="ResponseDailyCloseJson.Items"/>, each item exposes the server-derived opening value,
/// the current closing value, and whether it is the system-managed cash-variance product. This is
/// a separate DTO because <see cref="ResponseDailyCloseJson"/> is also returned by write operations,
/// where calculating or returning review-only opening values would make that shared contract
/// context-dependent.
/// </summary>
public class ResponseDailyCloseReviewJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
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
    public IReadOnlyList<ResponseDailyCloseReviewItemJson> Items { get; init; } = [];
}
