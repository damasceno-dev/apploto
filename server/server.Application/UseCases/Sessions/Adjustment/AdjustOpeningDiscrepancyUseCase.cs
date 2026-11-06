using server.Application.Services.Sessions;
using server.Application.Services.Receivables;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Adjustment;

public class AdjustOpeningDiscrepancyUseCase(
    BankCommandRunner runner,
    ISessionOpeningDiscrepanciesRepository discrepancies,
    IOpeningDiscrepancyAdjustmentsRepository adjustments,
    IDailyClosesRepository sessions,
    ILockDateReader lockDateReader,
    IBranchClock clock)
{
    public Task<ResponseOpeningDiscrepancyAdjustmentJson> Execute(
        RequestOpeningDiscrepancyAdjustmentJson request, string? key, CancellationToken ct = default)
    {
        var validation = new OpeningDiscrepancyAdjustmentFluentValidation().Validate(request);
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(e => e.ErrorMessage).ToList());
        return runner.Run<RequestOpeningDiscrepancyAdjustmentJson, ResponseOpeningDiscrepancyAdjustmentJson>(
            "POST /session/opening-discrepancy-adjustment", request, key,
            async (actor, token) =>
            {
                var source = await discrepancies.GetByIdAndBranchIdAsNoTracking(request.DiscrepancyId, actor.BranchId, token)
                    ?? throw new NotFoundException(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
                var session = await Session(source.SessionId, actor.BranchId, token);
                var predecessor = source.PredecessorSessionId is { } id ? await Session(id, actor.BranchId, token) : null;
                return predecessor is null ? [session.AccountId] : new[] { session.AccountId, predecessor.AccountId };
            },
            async (actor, token) =>
            {
                var source = await adjustments.LockSource(actor.BranchId, request.DiscrepancyId, token)
                    ?? throw new NotFoundException(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
                var previous = await adjustments.Latest(actor.BranchId, source.Id, token);
                var attribution = await OpeningAttributionReader.Read(discrepancies, source.Id, token);
                if (source.Version != request.ExpectedVersion || source.CurrentRevision != request.ComparisonRevision ||
                    (previous?.Sequence ?? 0) != request.ExpectedAdjustmentSequence || previous?.Id != request.PreviousAdjustmentId ||
                    attribution?.Id != request.PriorAttributionId) throw Stale();
                var session = await Session(source.SessionId, actor.BranchId, token);
                var predecessor = source.PredecessorSessionId is { } id ? await Session(id, actor.BranchId, token) : null;
                var lockDate = await lockDateReader.Read(actor.BranchId, token);
                // A still-open source uses the existing attribution command. A locked predecessor also requires
                // this append-only path; never mutate its responsibility indirectly from an open successor.
                if (session.Date > lockDate && (predecessor == null || predecessor.Date > lockDate))
                    throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_SOURCE_OPEN);
                var today = clock.LocalBusinessDate(clock.UtcNow());
                if (request.PostedOn <= lockDate)
                    throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_LOCKED);
                if (request.PostedOn < session.Date || request.PostedOn > today ||
                    (previous != null && request.PostedOn < previous.PostedOn))
                    throw new OnValidationException([ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_DATE_INVALID]);
                Guid? Target(OpeningDiscrepancyAttribution target) => target switch
                {
                    OpeningDiscrepancyAttribution.Incoming => session.CustodianOperatorId,
                    OpeningDiscrepancyAttribution.Outgoing => predecessor?.CustodianOperatorId,
                    _ => null
                };
                var person = Target(request.Target);
                if (request.Target != OpeningDiscrepancyAttribution.Neither && person == null)
                    throw new OnValidationException([ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_UNAVAILABLE]);
                var oldPerson = previous?.OperatorId ?? (previous == null && source.AttributedRevision == source.CurrentRevision
                    ? Target(source.Attribution) : null);
                var oldAmount = previous?.SignedAmount ?? (oldPerson == null ? 0m : source.CurrentTotalDelta);
                var newAmount = person == null ? 0m : source.CurrentTotalDelta;
                // A first Neither decision resolves unassigned evidence; an identical later decision does not.
                var priorTarget = previous?.Target ?? (source.AttributedRevision == source.CurrentRevision
                    ? source.Attribution : OpeningDiscrepancyAttribution.Unattributed);
                if (source.CurrentTotalDelta == 0 || oldPerson == person && oldAmount == newAmount &&
                    (person != null || priorTarget == request.Target))
                    throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_NO_CHANGE);
                var record = new OpeningDiscrepancyAdjustment
                {
                    BranchId = actor.BranchId, DiscrepancyId = source.Id, ComparisonRevision = source.CurrentRevision,
                    Sequence = (previous?.Sequence ?? 0) + 1, PreviousAdjustmentId = previous?.Id, PriorAttributionId = attribution?.Id,
                    SessionId = session.Id, PredecessorSessionId = source.PredecessorSessionId, HandoverId = source.HandoverId,
                    SourceDate = session.Date, PostedOn = request.PostedOn, Target = request.Target,
                    PreviousOperatorId = oldPerson, PreviousSignedAmount = oldAmount, OperatorId = person,
                    SignedAmount = newAmount,
                    ActorUserId = actor.UserId, CreatedAt = clock.UtcNow(), Reason = request.Reason.Trim()
                };
                await adjustments.Add(record, token);
                return (record.Id, record.ToResponse());
            }, ct);
    }

    private async Task<DailyClose> Session(Guid id, Guid branch, CancellationToken ct) =>
        await sessions.GetByIdAndBranchIdAsNoTracking(id, branch, ct)
        ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
    private static ConflictException Stale() => new(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
}
