using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IDailyCloseReviewEventsRepository
{
    Task Add(DailyCloseReviewEvent reviewEvent, CancellationToken ct = default);

    /// <summary>
    /// Every event of one close in the branch, with its items and the source/previous-source closes,
    /// ordered by <c>OccurredAt</c> then <c>Id</c>.
    /// </summary>
    Task<IReadOnlyList<DailyCloseReviewEvent>> ListByDailyCloseIdAndBranchIdAsNoTracking(
        Guid dailyCloseId,
        Guid branchId,
        CancellationToken ct = default);
}
