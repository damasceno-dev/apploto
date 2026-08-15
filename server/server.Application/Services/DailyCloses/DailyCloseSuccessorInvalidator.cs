using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

public sealed record DailyCloseSuccessorInvalidation(
    DailyClose DailyClose,
    DailyCloseStatus PreviousStatus,
    DateTime OpeningRecheckRequiredAt);

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

        if (successor is null || successor.Status == DailyCloseStatus.Draft)
            return null;

        if (successor.Status is not (DailyCloseStatus.Submitted or DailyCloseStatus.Approved or DailyCloseStatus.Rejected))
            return null;

        var previousStatus = successor.Status;

        // The predecessor already passed the lock-date guard. Because this selected successor is
        // strictly later, it cannot be inside the locked interval, so a second lock-date read would
        // be redundant and would weaken the single resolved-value boundary.
        draftTransition.ApplyOpeningRecheck(successor, triggeringClose, now, triggeringUserId);

        return new DailyCloseSuccessorInvalidation(successor, previousStatus, now);
    }
}
