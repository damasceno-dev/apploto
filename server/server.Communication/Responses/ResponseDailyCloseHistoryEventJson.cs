using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One immutable review event. Actor names are the display names captured when the event happened.
/// Rejection-only and opening-change-only members are null on the other type.
/// </summary>
public class ResponseDailyCloseHistoryEventJson
{
    public Guid Id { get; init; }
    public DailyCloseReviewEventType Type { get; init; }
    public DateTime OccurredAt { get; init; }

    public Guid ActorUserId { get; init; }
    public string ActorUserName { get; init; } = string.Empty;
    public Guid? ActorOperatorId { get; init; }
    public string? ActorOperatorName { get; init; }

    public string? RejectionReason { get; init; }

    /// <summary>The corrected close whose saved count changed this close's opening.</summary>
    public Guid? SourceDailyCloseId { get; init; }
    public DateTime? SourceDate { get; init; }

    /// <summary>The opening source before the change; equals the source unless a first count replaced it.</summary>
    public Guid? PreviousSourceDailyCloseId { get; init; }
    public DateTime? PreviousSourceDate { get; init; }

    public DailyCloseStatus? AffectedStatusBefore { get; init; }
    public DailyCloseStatus? AffectedStatusAfter { get; init; }

    public IReadOnlyList<ResponseDailyCloseOpeningChangeItemJson> Items { get; init; } = [];
}
