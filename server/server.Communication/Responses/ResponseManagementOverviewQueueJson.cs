using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 1: what still needs action, as counts from the Manager Work Queue for <see cref="Date"/> (its rules,
/// unchanged). Drill-down: the Work Queue, <c>GET /report/manager-queue?date={Date}</c>.
/// </summary>
public class ResponseManagementOverviewQueueJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime Date { get; init; }
    public int PendingApprovalCount { get; init; }
    public int RejectedCount { get; init; }
    public int NotSubmittedCount { get; init; }
    public int BacklogSubmittedCount { get; init; }
    public int BacklogRejectedCount { get; init; }
    public int BacklogDraftCount { get; init; }
    public DateTime? OldestPendingDate { get; init; }
    public int UnlockedDraftTransactionCount { get; init; }
    public int PendingOperatorLinkRequestCount { get; init; }
}
