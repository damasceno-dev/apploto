using System.Globalization;
using server.Application.Services.Idempotency;
using server.Application.Services.Members;
using server.Application.Services.Security;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Approve;

/// <summary>
/// ApproveSession and ApproveCashboxDay (§6.20, M9.6 Phase 5). The approver is neither recorder, submitter nor custodian
/// (<see cref="SessionApprovalPolicy"/>); <c>SingleActiveManager</c> is the explicit exception, available only while the
/// branch shows exactly one active Manager/Admin, and only with a reason. Inside the cashbox transaction the approval
/// holds the branch security lock shared — the lock every membership insert, reactivation, role change and removal
/// takes exclusively — and every existing membership row FOR SHARE, and only then re-reads the approver's membership and
/// counts managers, so both stay true until the commit. Every session is named with the version the manager read; the
/// batch validates every one under the cashbox coordination and commits all approvals, events and the idempotency
/// outcome together, or nothing. Approval never moves custody. The approver's current authority is checked before a stored outcome is replayed.
/// </summary>
public class ApproveSessionUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IBranchUsersRepository branchUsersRepository,
    ISecurityCoordination securityCoordination,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    IDailyCloseWorkflowGuard workflowGuard,
    ILockDateReader lockDateReader,
    LockDateGuard lockDateGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionEventRecorder eventRecorder,
    FinancialCommandIdempotency idempotency,
    IDailyCloseAccountCoordination accountCoordination,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    private const string SingleEndpoint = "POST /session/{id}/approve";
    private const string BatchEndpoint = "POST /session/approve-day";

    public async Task<ResponseDailyCloseJson> Execute(
        Guid sessionId,
        RequestApproveSessionJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new ApproveSessionFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var approved = await Approve(
            SingleEndpoint,
            idempotencyKey,
            new { SessionId = sessionId, Version = expectedVersion, request.Mode, request.Reason },
            [new RequestApproveSessionBatchItemJson { SessionId = sessionId, Version = expectedVersion }],
            request.Mode,
            request.Reason,
            batch: false,
            ct);
        return approved.Sessions[0];
    }

    public async Task<ResponseApproveSessionBatchJson> ExecuteBatch(
        RequestApproveSessionBatchJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new ApproveSessionBatchFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).Distinct().ToList());

        return await Approve(
            BatchEndpoint,
            idempotencyKey,
            request,
            request.Sessions!,
            request.Mode,
            request.Reason,
            batch: true,
            ct);
    }

    private async Task<ResponseApproveSessionBatchJson> Approve(
        string endpoint,
        string? idempotencyKey,
        object fingerprint,
        IReadOnlyList<RequestApproveSessionBatchItemJson> items,
        SessionApprovalMode mode,
        string? reason,
        bool batch,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (mode == SessionApprovalMode.SingleActiveManager && normalizedReason is null)
            throw new OnValidationException([ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_REQUIRES_REASON]);

        // Current authority first: a stored approval is never replayed to a caller who lost the manager role.
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not (Role.Manager or Role.Admin))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var keys = new List<DailyClose>();
        foreach (var item in items)
        {
            var key = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(item.SessionId, branchUser.BranchId, ct)
                ?? throw Rejected(batch, item.SessionId, new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND));
            keys.Add(key);
        }

        if (batch && keys.Select(key => (key.AccountId, key.Date.Date)).Distinct().Count() > 1)
            throw new OnValidationException([ResourcesErrorMessages.SESSION_BATCH_APPROVAL_SCOPE_MIXED]);

        var utcNow = branchClock.UtcNow();
        var replay = await idempotency.TryReplay<object, ResponseApproveSessionBatchJson>(
            idempotencyKey,
            endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (replay is not null)
            return replay;

        await using var coordination = await accountCoordination.AcquireMany(
            branchUser.BranchId,
            keys.Select(key => key.AccountId).Distinct().ToList(),
            ct);

        // Membership writers wait for this commit, and this waits for theirs: the approver's role and the manager count
        // read below are the ones in force when the approval commits.
        await securityCoordination.HoldSharedInCurrentTransaction(SecurityRules.BranchKey(branchUser.BranchId), ct);
        await branchUsersRepository.LockMembershipsByBranchId(branchUser.BranchId, ct);
        branchUser = await SecurityRules.FreshActor(branchUsersRepository, branchUser);

        var context = await idempotency.Prepare<object, ResponseApproveSessionBatchJson>(
            idempotencyKey!,
            endpoint,
            branchUser.BranchId,
            branchUser.UserId,
            fingerprint,
            utcNow,
            ct);
        if (context.IsReplay)
        {
            await coordination.Complete(ct);
            return context.ReplayResponse!;
        }

        var scope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        var lockDate = await lockDateReader.Read(branchUser.BranchId, ct);

        int? activeManagers = mode == SessionApprovalMode.SingleActiveManager
            ? await branchUsersRepository.CountActiveManagersByBranchIdAsNoTracking(branchUser.BranchId, ct)
            : null;

        var sessions = new List<DailyClose>();
        var failures = new List<(Guid SessionId, ServerException Error)>();
        foreach (var item in items)
        {
            var session = await dailyClosesRepository.GetByIdAndBranchId(item.SessionId, branchUser.BranchId, ct);
            try
            {
                if (session is null)
                    throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
                workflowGuard.EnsureCanApprove(session, branchUser);
                lockDateGuard.EnsureNotLocked(session.Date, lockDate, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
                if (session.Version != item.Version)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);

                var conflict = SessionApprovalPolicy.DutyConflict(session, branchUser.UserId, scope.LinkedOperator);
                if (mode == SessionApprovalMode.Independent && conflict is not null)
                    throw new TokenWithoutPermissionException(conflict);
                if (mode == SessionApprovalMode.SingleActiveManager && activeManagers != 1)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_NOT_AVAILABLE);

                sessions.Add(session!);
            }
            catch (ServerException error)
            {
                failures.Add((item.SessionId, error));
            }
        }

        // All or nothing: one ineligible session refuses the whole batch, with every offending session named.
        if (failures.Count > 0)
        {
            if (batch is false)
                throw failures[0].Error;
            throw new BatchRejectedException(
                failures[0].Error.GetStatusCode,
                failures.SelectMany(failure => failure.Error.GetErrorMessages.Select(message => Format(failure.SessionId, message))).ToList());
        }

        var now = branchClock.UtcNow();
        var batchId = batch ? Guid.NewGuid() : (Guid?)null;
        foreach (var session in sessions)
        {
            session.Status = DailyCloseStatus.Approved;
            session.ApprovedAt = now;
            session.ApprovedByUserId = branchUser.UserId;
            session.UpdatedAt = now;
            session.UpdatedByUserId = branchUser.UserId;
            await eventRecorder.Record(
                session,
                SessionEventKind.SessionApproved,
                branchUser.User,
                now,
                new
                {
                    Mode = mode,
                    BatchId = batchId,
                    BatchSessionIds = batch ? sessions.Select(item => item.Id).ToList() : null,
                    ActiveManagerCount = activeManagers,
                    session.SubmittedAttemptId
                },
                reason: normalizedReason,
                ct: ct);
        }

        await unitOfWork.Commit(ct);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId, ct);
        var response = new ResponseApproveSessionBatchJson();
        var reloaded = new List<ResponseDailyCloseJson>();
        foreach (var session in sessions)
        {
            var current = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(session.Id, branchUser.BranchId, ct);
            reloaded.Add(current!.ToResponse(cashVarianceProductId));
        }
        response = new ResponseApproveSessionBatchJson { Sessions = reloaded };

        FinancialCommandIdempotency.Complete(context, sessions[0].Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);
        return response;
    }

    private static ServerException Rejected(bool batch, Guid sessionId, ServerException error)
    {
        return batch
            ? new BatchRejectedException(error.GetStatusCode, error.GetErrorMessages.Select(message => Format(sessionId, message)).ToList())
            : error;
    }

    private static string Format(Guid sessionId, string message)
    {
        return string.Format(CultureInfo.InvariantCulture, ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, sessionId, message);
    }
}
