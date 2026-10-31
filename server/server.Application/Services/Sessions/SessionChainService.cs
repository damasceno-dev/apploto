using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;

namespace server.Application.Services.Sessions;

public sealed record SessionChainEffect(DailyClose Session, bool OpeningRecheckRequired, bool DiscrepancyRevised);

/// <summary>
/// Keeps a cashbox's Sequence equal to its interval order after a declared or rearranged past session (owner
/// decision F2) and applies the §6.20 correction rules to every session whose counted predecessor changed — at the
/// old and the new position: an acknowledged successor gets a discrepancy comparison revision, an inherited one the
/// opening-recheck demotion. A locked affected session refuses the whole command. The caller holds the cashbox
/// coordination; renumbering is two-phase so the unique Sequence index never sees a transient duplicate.
/// </summary>
public sealed class SessionChainService(
    IDailyCloseDraftTransition draftTransition,
    OpeningDiscrepancyRecorder discrepancyRecorder,
    ICashVarianceProductResolver cashVarianceProductResolver,
    LockDateGuard lockDateGuard,
    IUnitOfWork unitOfWork)
{
    private const int SequenceOffset = 1_000_000;

    public async Task<IReadOnlyList<SessionChainEffect>> Resequence(
        IReadOnlyList<DailyClose> chain,
        DailyClose changed,
        User actor,
        DateTime now,
        CancellationToken ct = default)
    {
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(changed.BranchId, ct);
        var before = CountedPredecessors(chain.Where(session => session.Sequence is not null).OrderBy(session => session.Sequence));

        var ordered = chain
            .OrderBy(session => session.StartedAt)
            .ThenBy(session => session.Sequence ?? int.MaxValue)
            .ToList();
        var after = CountedPredecessors(ordered);

        // Phase 1 moves every changing Sequence out of the occupied range; phase 2 writes the final order.
        var targets = ordered.Select((session, index) => (Session: session, Sequence: index + 1)).ToList();
        var moving = targets.Where(target => target.Session.Sequence != target.Sequence).ToList();
        if (moving.Count > 0)
        {
            foreach (var target in moving)
                target.Session.Sequence = target.Sequence + SequenceOffset;
            await unitOfWork.Commit(ct);
            foreach (var target in moving)
                target.Session.Sequence = target.Sequence;
        }

        // An inherited opening follows its counted predecessor, the changed session's own included.
        if (before.TryGetValue(changed.Id, out var changedPrevious) is false || changedPrevious?.Id != after[changed.Id]?.Id)
            SessionRevisionService.RecordInheritedOpeningChange(changed);

        var effects = new List<SessionChainEffect>();
        foreach (var session in ordered.Where(session => session.Id != changed.Id))
        {
            before.TryGetValue(session.Id, out var previousPredecessor);
            var currentPredecessor = after[session.Id];
            if (previousPredecessor?.Id == currentPredecessor?.Id)
                continue;
            SessionRevisionService.RecordInheritedOpeningChange(session);
            if (session.ItemsFirstRecordedAt is null && session.Opening is null)
                continue;

            await lockDateGuard.EnsureNotLocked(
                session.BranchId,
                session.Date,
                ResourcesErrorMessages.SESSION_LOCKED_SUCCESSOR,
                ct);

            if (session.Opening is not null)
            {
                if (currentPredecessor is null)
                    continue;
                await discrepancyRecorder.ReviseForPredecessorChange(
                    session,
                    currentPredecessor,
                    SessionOpeningResolver.ClosingValues(currentPredecessor, cashVarianceProductId),
                    actor,
                    now,
                    ct);
                effects.Add(new SessionChainEffect(session, false, true));
                continue;
            }

            if (session.Status is DailyCloseStatus.Submitted or DailyCloseStatus.Approved or DailyCloseStatus.Rejected)
            {
                draftTransition.ApplyOpeningRecheck(session, currentPredecessor ?? changed, now, actor.Id);
                effects.Add(new SessionChainEffect(session, true, false));
            }
        }

        return effects;
    }

    private static Dictionary<Guid, DailyClose?> CountedPredecessors(IEnumerable<DailyClose> ordered)
    {
        var result = new Dictionary<Guid, DailyClose?>();
        DailyClose? latestCounted = null;
        foreach (var session in ordered)
        {
            result[session.Id] = latestCounted;
            if (session.ItemsFirstRecordedAt is not null)
                latestCounted = session;
        }

        return result;
    }
}
