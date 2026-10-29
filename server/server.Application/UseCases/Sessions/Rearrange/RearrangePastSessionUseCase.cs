using FluentValidation;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Rearrange;

/// <summary>
/// RearrangePastSession (owner decision F2): a Manager/Admin moves the declared interval — and so the business date —
/// of a Finished/HandedOver Draft session. Its cashbox rows and their generated pair's source rows move to the new
/// date in the same transaction (DueDate kept); Sequence follows interval order; successors at the old and the new
/// position follow the correction rules. Count items are corrected through the ordinary count save. Active custody
/// and an operator-acknowledged start are never moved. All or nothing.
/// </summary>
public class RearrangePastSessionUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository,
    SessionChainService chainService,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock)
{
    public async Task<ResponseSessionChainChangeJson> Execute(
        Guid sessionId,
        RequestRearrangePastSessionJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var validation = new RearrangePastSessionFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);

        return await commandRunner.RunGuarded<ResponseSessionChainChangeJson>(
            branchUser,
            [sessionKey.AccountId],
            async () =>
            {
                if (await dailyClosesRepository.HasUnresolvedHistoryByAccountIdAsNoTracking(branchUser.BranchId, sessionKey.AccountId, ct))
                    throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_HISTORY);

                var chain = await dailyClosesRepository.ListReadyByAccountIdOrderedByStart(branchUser.BranchId, sessionKey.AccountId, ct);
                var session = chain.FirstOrDefault(candidate => candidate.Id == sessionId)
                    ?? throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
                if (session.Version != expectedVersion)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);
                if (session.CustodyState is not (SessionCustodyState.Finished or SessionCustodyState.HandedOver)
                    || session.Status != DailyCloseStatus.Draft
                    || session.Opening is { EvidenceKind: SessionOpeningEvidenceKind.IncomingAcknowledgment })
                {
                    throw new ConflictException(ResourcesErrorMessages.SESSION_REARRANGE_NOT_ALLOWED);
                }

                var (startedAt, endedAt, businessDate) = SessionWindow.Resolve(request.StartedAt, request.EndedAt, branchClock);
                if (chain.Any(other => other.Id != session.Id
                        && SessionWindow.Overlaps(other.StartedAt!.Value, other.EndedAt, startedAt, endedAt)))
                {
                    throw new ConflictException(ResourcesErrorMessages.SESSION_DECLARE_OVERLAP);
                }

                var previousDate = session.Date;
                await lockDateGuard.EnsureNotLocked(branchUser.BranchId, previousDate, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION, ct);
                await lockDateGuard.EnsureNotLocked(branchUser.BranchId, businessDate, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION, ct);

                var movedIds = new List<Guid>();
                if (businessDate != previousDate)
                {
                    var rows = await transactionsRepository.ListActiveBySessionId(branchUser.BranchId, session.Id, ct);
                    foreach (var row in rows)
                    {
                        if (row.PaidAt is not null)
                            throw new ConflictException(ResourcesErrorMessages.SESSION_REARRANGE_RECONCILED_ROW);
                        row.Date = businessDate;
                        movedIds.Add(row.Id);

                        if (row.GeneratedFromTransactionId is { } sourceId)
                        {
                            var source = await transactionsRepository.GetByIdAndBranchId(sourceId, branchUser.BranchId, ct)
                                ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
                            if (source.PaidAt is not null)
                                throw new ConflictException(ResourcesErrorMessages.SESSION_REARRANGE_RECONCILED_ROW);
                            source.Date = businessDate;
                            movedIds.Add(source.Id);
                        }
                    }
                }

                var before = new { session.StartedAt, session.EndedAt, session.Date, session.Sequence };
                var now = branchClock.UtcNow();
                session.StartedAt = startedAt;
                session.EndedAt = endedAt;
                session.Date = businessDate;
                session.UpdatedAt = now;
                session.UpdatedByUserId = branchUser.UserId;

                var effects = await chainService.Resequence(chain, session, branchUser.User, now, ct);
                await eventRecorder.Record(
                    session,
                    SessionEventKind.SessionRearranged,
                    branchUser.User,
                    now,
                    new
                    {
                        Before = before,
                        After = new { session.StartedAt, session.EndedAt, session.Date, session.Sequence },
                        MovedTransactionIds = movedIds,
                        Affected = effects.Select(effect => effect.Session.Id)
                    },
                    request.Reason!.Trim(),
                    ct: ct);

                return async () => new ResponseSessionChainChangeJson
                {
                    Session = await responseLoader.Load(session.Id, branchUser.BranchId, ct),
                    MovedTransactionIds = movedIds,
                    AffectedSessions = await SessionChainResponses.Load(effects, dailyClosesRepository, branchUser.BranchId, ct)
                };
            },
            ct);
    }
}

public class RearrangePastSessionFluentValidation : FluentValidation.AbstractValidator<RequestRearrangePastSessionJson>
{
    public RearrangePastSessionFluentValidation()
    {
        RuleFor(request => request.Reason).RequiredReason();
        RuleFor(request => request)
            .Must(request => request.EndedAt > request.StartedAt)
            .WithMessage(ResourcesErrorMessages.SESSION_DECLARE_WINDOW_INVALID);
    }
}
