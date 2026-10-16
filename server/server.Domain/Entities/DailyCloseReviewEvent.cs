using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// Immutable review history of one close (gap X): a rejection of the close itself, or a change
/// to its opening caused by a real count change on its opening source. Captured in the same
/// transaction as the command that caused it; never updated or derived later. Actor and product
/// names are snapshots taken when the event happened.
/// </summary>
public class DailyCloseReviewEvent : EntityBase
{
    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    /// <summary>The close this event explains: the rejected close, or the successor whose opening changed.</summary>
    public Guid DailyCloseId { get; init; }
    public DailyClose DailyClose { get; init; } = null!;

    public DailyCloseReviewEventType Type { get; init; }
    public DateTime OccurredAt { get; init; }

    public Guid ActorUserId { get; init; }
    public User ActorUser { get; init; } = null!;
    public string ActorUserName { get; init; } = string.Empty;
    public Guid? ActorOperatorId { get; init; }
    public Operator? ActorOperator { get; init; }
    public string? ActorOperatorName { get; init; }

    /// <summary>Rejected only: the reviewer's reason as given at rejection time.</summary>
    public string? RejectionReason { get; init; }

    /// <summary>OpeningChanged only: the close whose saved count changed (the opening source after the save).</summary>
    public Guid? SourceDailyCloseId { get; init; }
    public DailyClose? SourceDailyClose { get; init; }

    /// <summary>OpeningChanged only: the source close's version the saved count was applied to.</summary>
    public uint? SourceVersion { get; init; }

    /// <summary>OpeningChanged only: the opening source before the save; null when there was none.</summary>
    public Guid? PreviousSourceDailyCloseId { get; init; }
    public DailyClose? PreviousSourceDailyClose { get; init; }

    /// <summary>OpeningChanged only: the affected close's status before and after the same save.</summary>
    public DailyCloseStatus? AffectedStatusBefore { get; init; }
    public DailyCloseStatus? AffectedStatusAfter { get; init; }

    public ICollection<DailyCloseReviewEventItem> Items { get; init; } = [];
}
