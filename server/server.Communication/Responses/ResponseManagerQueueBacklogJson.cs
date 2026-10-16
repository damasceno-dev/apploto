namespace server.Communication.Responses;

/// <summary>
/// Pending closes on unlocked days before the manager queue's requested date. Approved closes never count.
/// </summary>
public class ResponseManagerQueueBacklogJson
{
    public int SubmittedCount { get; init; }
    public int RejectedCount { get; init; }
    public int DraftCount { get; init; }

    /// <summary>Earliest day carrying a counted close; null when the backlog is empty.</summary>
    public DateTime? OldestPendingDate { get; init; }
}
