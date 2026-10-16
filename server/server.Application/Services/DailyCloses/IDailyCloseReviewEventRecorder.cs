using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseReviewEventRecorder
{
    Task RecordRejection(
        DailyClose rejectedClose,
        string rejectionReason,
        User actor,
        Operator? actorOperator,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// Records that <paramref name="successor"/>'s opening changed because <paramref name="source"/>'s
    /// saved count changed. <paramref name="openingBefore"/> is the successor's opening as it stood before
    /// the save (the previous source's items, or the source's own items when it was already the source);
    /// <paramref name="openingAfter"/> is the source's saved count. Products with no saved item are absent
    /// from the maps, which is distinct from a recorded zero.
    /// </summary>
    Task RecordOpeningChange(
        DailyClose source,
        uint sourceVersion,
        Guid? previousSourceDailyCloseId,
        IReadOnlyDictionary<Guid, decimal> openingBefore,
        IReadOnlyDictionary<Guid, decimal> openingAfter,
        DailyCloseSuccessorInvalidation successor,
        User actor,
        Operator? actorOperator,
        DateTime occurredAt,
        CancellationToken ct = default);
}
