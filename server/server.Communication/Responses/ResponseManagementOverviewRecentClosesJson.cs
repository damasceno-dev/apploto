using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 2: closes dated <c>[AsOfDate − 6, AsOfDate]</c>, every status. Whole-window counts; the preview follows
/// <c>GET /dailyclose</c> order. Variance is official only (non-Draft persisted), else null.
/// </summary>
public class ResponseManagementOverviewRecentClosesJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int TotalCount { get; init; }
    public int DraftCount { get; init; }
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
    public int PreviewLimit { get; init; }
    public IReadOnlyList<ResponseManagementOverviewCloseJson> Items { get; init; } = [];
}
