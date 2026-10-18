namespace server.Communication.Responses;

/// <summary>
/// Manager work-queue snapshot for one branch-local date: the day's close rows with variance,
/// the pending-approval count, branch-level variance aggregates, and the expected accounts
/// that have not submitted a close yet. <see cref="Backlog"/> and
/// <see cref="UnlockedDraftTransactionCount"/> look past the requested date and deliberately
/// use different upper bounds.
/// </summary>
public class ResponseManagerQueueJson
{
    public DateTime Date { get; init; }
    public decimal TotalVariance { get; init; }
    public decimal MeanVariance { get; init; }
    public int PendingOperatorLinkRequestCount { get; init; }
    public int PendingApprovalCount { get; init; }
    public IReadOnlyList<ResponseManagerQueueCloseJson> Closes { get; init; } = [];
    public IReadOnlyList<ResponseManagerQueueNotSubmittedJson> NotSubmitted { get; init; } = [];

    /// <summary>Pending closes with <c>LockDate &lt; Date &lt;</c> the requested date.</summary>
    public ResponseManagerQueueBacklogJson Backlog { get; init; } = new();

    /// <summary>Draft transactions with <c>LockDate &lt; Date ≤</c> branch-local today, whatever date was requested.</summary>
    public int UnlockedDraftTransactionCount { get; init; }
}
