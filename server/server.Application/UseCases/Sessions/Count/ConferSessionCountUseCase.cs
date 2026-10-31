using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Members;
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

namespace server.Application.UseCases.Sessions.Count;

/// <summary>The persisted replay envelope of a Conferir key: only the attempt identity, the response is rebuilt fresh.</summary>
public sealed record SessionCountAttemptReplay(Guid SessionId, Guid AttemptId);

/// <summary>
/// Conferir (§6.20 ConferSessionCount, M9.6 Phase 4). Identity is the server token, not the request key: after the
/// caller's current read authority, an already-conferred token returns its own immutable attempt (same fingerprint)
/// even under another Idempotency-Key or an old root version, and a changed fingerprint is 409. An unused token needs
/// current edit authority, Draft, the current token, version and revisions, and commits the attempt, its calculation,
/// the CountConferred event and the replay outcome in one transaction. A failure consumes neither token nor number.
/// The response always carries the current candidate state, so a replay never presents an old attempt as current.
/// </summary>
public class ConferSessionCountUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    ISessionCountAttemptsRepository attemptsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    IDailyCloseLedgerGuard ledgerGuard,
    LockDateGuard lockDateGuard,
    SessionCountService countService,
    SessionEventRecorder eventRecorder,
    SessionCountProjector projector,
    FinancialCommandIdempotency idempotency,
    IDailyCloseAccountCoordination accountCoordination,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    private const string Endpoint = "POST /session/{id}/conferir";

    public async Task<ResponseConferSessionCountJson> Execute(
        Guid sessionId,
        RequestConferSessionCountJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        ct.ThrowIfCancellationRequested();

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        EnsureCanRead(branchUser, scope, sessionKey);

        var fingerprint = new { SessionId = sessionId, Request = request };
        var utcNow = branchClock.UtcNow();
        var stored = await idempotency.TryReplay<object, SessionCountAttemptReplay>(
            idempotencyKey,
            Endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (stored is not null)
            return await Replay(stored.AttemptId, sessionId, branchUser, scope, ct);

        await using var coordination = await accountCoordination.Acquire(branchUser.BranchId, sessionKey.AccountId, ct);
        var context = await idempotency.Prepare<object, SessionCountAttemptReplay>(
            idempotencyKey!,
            Endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (context.IsReplay)
        {
            await coordination.Complete(ct);
            return await Replay(context.ReplayResponse!.AttemptId, sessionId, branchUser, scope, ct);
        }

        var session = await dailyClosesRepository.GetByIdAndBranchId(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        EnsureCanRead(branchUser, scope, session);

        // An already-conferred token resolves before any edit, state or root-version rule (§6.20).
        var used = await attemptsRepository.GetBySessionIdAndTokenAsNoTracking(
            branchUser.BranchId,
            session.Id,
            request.ConfirmationToken,
            ct);
        if (used is not null)
        {
            EnsureSameFingerprint(used, request);
            FinancialCommandIdempotency.Complete(context, used.Id, new SessionCountAttemptReplay(session.Id, used.Id));
            await unitOfWork.Commit(ct);
            await coordination.Complete(ct);
            return await Replay(used.Id, sessionId, branchUser, scope, ct);
        }

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, scope, session.AccountId);
        workflowGuard.EnsureCanEditItems(session, branchUser, scope.LinkedOperator);
        // Draft, or Rejected awaiting correction (a session submitted before Phase 4 has no attempt to resubmit).
        if (session.Status is not (DailyCloseStatus.Draft or DailyCloseStatus.Rejected))
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_REQUIRES_DRAFT);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            session.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);
        if (session.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);
        if (session.CurrentConfirmationToken != request.ConfirmationToken)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_STALE);
        if (session.CountRevision != request.CountRevision
            || session.LedgerRevision != request.LedgerRevision
            || session.OpeningRevision != request.OpeningRevision)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_REVISION_STALE);
        if (session.ItemsFirstRecordedAt is null)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ITEMS_NOT_RECORDED);
        await ledgerGuard.EnsureNoOutstandingDraftTransactions(session, ct);

        var latest = await countService.GetLatestAttempt(session, ct);
        if (SessionCountService.RequiresStillObserved(session, latest) && request.ConfirmCountStillObserved is false)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRM_STILL_OBSERVED_REQUIRED);

        var calculation = await countService.Calculate(session, ct);
        var now = branchClock.UtcNow();
        var attempt = new SessionCountAttempt
        {
            BranchId = session.BranchId,
            SessionId = session.Id,
            AttemptNumber = session.LastAttemptNumber + 1,
            ConfirmationToken = session.CurrentConfirmationToken,
            Kind = SessionCountService.KindOfNext(session, latest),
            CountRevision = session.CountRevision,
            LedgerRevision = session.LedgerRevision,
            OpeningRevision = session.OpeningRevision,
            TotalOpening = calculation.TotalOpening,
            TotalIn = calculation.TotalIn,
            TotalOut = calculation.TotalOut,
            ExpectedClosing = calculation.ExpectedClosing,
            TotalClosing = calculation.TotalClosing,
            Difference = calculation.Difference,
            ConferredByUserId = branchUser.UserId,
            ConferredByName = branchUser.User.Name,
            ConferredByOperatorId = scope.LinkedOperator?.Id,
            CommandKey = idempotencyKey!,
            CreatedAt = now,
            Items = calculation.Items
                .Select(item => new SessionCountAttemptItem
                {
                    ProductId = item.ProductId,
                    OpeningValue = item.OpeningValue,
                    CountedValue = item.CountedValue,
                    CreatedAt = now
                })
                .ToList()
        };
        await attemptsRepository.Add(attempt, ct);

        session.LastAttemptNumber = attempt.AttemptNumber;
        session.RecountStartedForAttemptId = null;
        session.UpdatedAt = now;
        session.UpdatedByUserId = branchUser.UserId;

        await eventRecorder.Record(
            session,
            SessionEventKind.CountConferred,
            branchUser.User,
            now,
            new
            {
                attempt.AttemptNumber,
                attempt.Kind,
                attempt.CountRevision,
                attempt.LedgerRevision,
                attempt.OpeningRevision,
                ConfirmedStillObserved = attempt.Kind == SessionCountAttemptKind.StillObserved
            },
            relatedId: attempt.Id,
            ct: ct,
            attemptId: attempt.Id);

        FinancialCommandIdempotency.Complete(context, attempt.Id, new SessionCountAttemptReplay(session.Id, attempt.Id));
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return await BuildResponse(attempt.Id, sessionId, branchUser, scope, replayed: false, ct);
    }

    private Task<ResponseConferSessionCountJson> Replay(
        Guid attemptId,
        Guid sessionId,
        BranchUser branchUser,
        MemberAccountScope scope,
        CancellationToken ct)
    {
        return BuildResponse(attemptId, sessionId, branchUser, scope, replayed: true, ct);
    }

    private async Task<ResponseConferSessionCountJson> BuildResponse(
        Guid attemptId,
        Guid sessionId,
        BranchUser branchUser,
        MemberAccountScope scope,
        bool replayed,
        CancellationToken ct)
    {
        var attempt = await attemptsRepository.GetByIdAndSessionIdAsNoTracking(branchUser.BranchId, sessionId, attemptId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        return new ResponseConferSessionCountJson
        {
            Attempt = attempt.ToResponse(),
            Replayed = replayed,
            Session = await projector.Load(sessionId, branchUser, scope, ct)
        };
    }

    private static void EnsureSameFingerprint(SessionCountAttempt used, RequestConferSessionCountJson request)
    {
        if (used.CountRevision != request.CountRevision
            || used.LedgerRevision != request.LedgerRevision
            || used.OpeningRevision != request.OpeningRevision)
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_CONFLICT);
    }

    internal static void EnsureCanRead(BranchUser branchUser, MemberAccountScope scope, DailyClose session)
    {
        if (branchUser.Role is not Role.Member)
            return;
        if (scope.LinkedOperator is null)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        if (scope.AllowedAccountIds.Contains(session.AccountId) is false)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    private static void Validate(RequestConferSessionCountJson request)
    {
        var result = new ConferSessionCountFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
