namespace server.Communication.Responses;

/// <summary>
/// Manager work-queue snapshot for one branch-local date: the day's close rows with variance,
/// the pending-approval count, branch-level variance aggregates, and the expected accounts
/// that have not submitted a close yet.
/// </summary>
public class ResponseDashboardJson
{
    public DateTime Date { get; init; }
    public decimal TotalVariance { get; init; }
    public decimal MeanVariance { get; init; }
    public int PendingApprovalCount { get; init; }
    public IReadOnlyList<ResponseDashboardCloseJson> Closes { get; init; } = [];
    public IReadOnlyList<ResponseDashboardNotSubmittedJson> NotSubmitted { get; init; } = [];
}
