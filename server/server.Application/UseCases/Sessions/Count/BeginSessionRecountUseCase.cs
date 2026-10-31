using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Count;

public sealed record SessionRecountReplay(Guid SessionId, Guid PreviousAttemptId);

/// <summary>
/// Recontar (§6.20 BeginRecount): deliberately rotates the confirmation token from the latest attempt, even when the
/// values will stay identical, hides its calculation and records RecountStarted. Repeating it for the same attempt is a
/// no-op; the attempt itself is never touched.
/// </summary>
public class BeginSessionRecountUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    LockDateGuard lockDateGuard,
    SessionCountService countService,
    SessionEventRecorder eventRecorder,
    SessionCountProjector projector,
    FinancialCommandIdempotency idempotency,
    IDailyCloseAccountCoordination accountCoordination,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    private const string Endpoint = "POST /session/{id}/recount";

    public async Task<ResponseDailyCloseJson> Execute(
        Guid sessionId,
        RequestBeginSessionRecountJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new BeginSessionRecountFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());
        ct.ThrowIfCancellationRequested();

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, scope, sessionKey.AccountId);
        workflowGuard.EnsureCanEditItems(sessionKey, branchUser, scope.LinkedOperator);

        var fingerprint = new { SessionId = sessionId, Request = request };
        var utcNow = branchClock.UtcNow();
        var stored = await idempotency.TryReplay<object, SessionRecountReplay>(
            idempotencyKey,
            Endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (stored is not null)
            return await projector.Load(sessionId, branchUser, scope, ct);

        await using var coordination = await accountCoordination.Acquire(branchUser.BranchId, sessionKey.AccountId, ct);
        var context = await idempotency.Prepare<object, SessionRecountReplay>(
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
            return await projector.Load(sessionId, branchUser, scope, ct);
        }

        var session = await dailyClosesRepository.GetByIdAndBranchId(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, scope, session.AccountId);
        workflowGuard.EnsureCanEditItems(session, branchUser, scope.LinkedOperator);
        if (session.Status is not (DailyCloseStatus.Draft or DailyCloseStatus.Rejected))
            throw new ConflictException(ResourcesErrorMessages.SESSION_CONFIRMATION_REQUIRES_DRAFT);
        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            session.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);

        var latest = await countService.GetLatestAttempt(session, ct)
            ?? throw new ConflictException(ResourcesErrorMessages.SESSION_RECOUNT_NOT_AVAILABLE);
        if (latest.Id != request.PreviousAttemptId)
            throw new ConflictException(ResourcesErrorMessages.SESSION_RECOUNT_ATTEMPT_MISMATCH);

        if (session.RecountStartedForAttemptId != latest.Id)
        {
            if (session.Version != expectedVersion)
                throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);

            var now = branchClock.UtcNow();
            SessionRevisionService.BeginRecount(session, latest.Id);
            session.UpdatedAt = now;
            session.UpdatedByUserId = branchUser.UserId;
            await eventRecorder.Record(
                session,
                SessionEventKind.RecountStarted,
                branchUser.User,
                now,
                new { PreviousAttemptNumber = latest.AttemptNumber },
                relatedId: latest.Id,
                ct: ct,
                attemptId: latest.Id);
        }

        FinancialCommandIdempotency.Complete(context, session.Id, new SessionRecountReplay(session.Id, latest.Id));
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return await projector.Load(sessionId, branchUser, scope, ct);
    }
}
