using server.Application.Services.Sessions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;

namespace server.Application.Services.DailyCloses;

/// <summary>
/// The next dependent session of a session whose counted input changed. <see cref="OpeningRecheckRequiredAt"/> is set
/// only when this save demoted the successor; it is null when the successor was already Draft and was left
/// untouched (its inherited opening still changed, which the review event records).
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
    IDailyCloseDraftTransition draftTransition,
    OpeningDiscrepancyRecorder discrepancyRecorder,
    Settings.LockDateGuard lockDateGuard) : IDailyCloseSuccessorInvalidator
{
    public async Task<DailyCloseSuccessorInvalidation?> InvalidateNextEligible(
        DailyClose triggeringClose,
        IReadOnlyDictionary<Guid, decimal> newClosingValues,
        DateTime now,
        User triggeringUser,
        CancellationToken ct = default)
    {
        var successor = await dailyClosesRepository.GetDependentSuccessor(triggeringClose, ct);
        if (successor is null)
            return null;

        // A locked dependent successor rejects the originating correction: locked history is never modified.
        await lockDateGuard.EnsureNotLocked(
            successor.BranchId,
            successor.Date,
            ResourcesErrorMessages.SESSION_LOCKED_SUCCESSOR,
            ct);

        // An acknowledged opening is immutable: the correction appends a discrepancy comparison revision and never
        // demotes the successor or changes its variance (§6.20 *Session opening in the §6.12 formula*).
        if (successor.Opening is not null)
        {
            await discrepancyRecorder.ReviseForPredecessorChange(
                successor,
                triggeringClose,
                newClosingValues,
                triggeringUser,
                now,
                ct);
            return null;
        }

        // The inherited opening moves with the predecessor's count: any confirmed candidate of the successor is stale.
        SessionRevisionService.RecordInheritedOpeningChange(successor);

        var previousStatus = successor.Status;
        if (previousStatus is not (DailyCloseStatus.Submitted or DailyCloseStatus.Approved or DailyCloseStatus.Rejected))
            return new DailyCloseSuccessorInvalidation(successor, previousStatus, null);

        draftTransition.ApplyOpeningRecheck(successor, triggeringClose, now, triggeringUser.Id);

        return new DailyCloseSuccessorInvalidation(successor, previousStatus, now);
    }
}
