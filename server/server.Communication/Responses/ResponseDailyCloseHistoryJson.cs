namespace server.Communication.Responses;

/// <summary>
/// Review history of one close (gap X): its own rejections and the changes to its opening, oldest
/// first, plus the close that is its opening source now.
/// </summary>
public class ResponseDailyCloseHistoryJson
{
    public Guid DailyCloseId { get; init; }

    /// <summary>
    /// False for a close that existed before review events were captured: an earlier rejection or
    /// opening change may be missing from <see cref="Events"/> and must be shown as unavailable.
    /// </summary>
    public bool IsHistoryComplete { get; init; }

    /// <summary>The current opening source; null when no earlier counted close exists.</summary>
    public ResponseDailyCloseOpeningSourceJson? CurrentOpeningSource { get; init; }

    public IReadOnlyList<ResponseDailyCloseHistoryEventJson> Events { get; init; } = [];
}
