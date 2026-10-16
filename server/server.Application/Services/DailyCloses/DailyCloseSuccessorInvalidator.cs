using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

/// <summary>
/// The next eligible successor of a close whose counted input changed. <see cref="OpeningRecheckRequiredAt"/>
/// is set only when this save demoted the successor; it is null when the successor was already Draft and
/// was left untouched (its opening still changed, which the review event records).
/// </summary>
public sealed record DailyCloseSuccessorInvalidation(
    DailyClose DailyClose,
    DailyCloseStatus PreviousStatus,
    DateTime? OpeningRecheckRequiredAt)
{
    public bool Demoted => OpeningRecheckRequiredAt is not null;
}

public sealed class DailyCloseSuccessorInvalidator(
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseDraftTransition draftTransition) : IDailyCloseSuccessorInvalidator
{
    public async Task<DailyCloseSuccessorInvalidation?> InvalidateNextEligible(
        DailyClose triggeringClose,
        DateTime now,
        Guid triggeringUserId,
        CancellationToken ct = default)
    {
        var successor = await dailyClosesRepository.GetNextEligibleAfterDateByBranchIdAndAccountId(
            triggeringClose.BranchId,
            triggeringClose.AccountId,
            triggeringClose.Date,
            ct);

        if (successor is null)
            return null;

        var previousStatus = successor.Status;
        if (previousStatus is not (DailyCloseStatus.Submitted or DailyCloseStatus.Approved or DailyCloseStatus.Rejected))
            return new DailyCloseSuccessorInvalidation(successor, previousStatus, null);

        // The predecessor already passed the lock-date guard. Because this selected successor is
        // strictly later, it cannot be inside the locked interval, so a second lock-date read would
        // be redundant and would weaken the single resolved-value boundary.
        draftTransition.ApplyOpeningRecheck(successor, triggeringClose, now, triggeringUserId);

        return new DailyCloseSuccessorInvalidation(successor, previousStatus, now);
    }
}
