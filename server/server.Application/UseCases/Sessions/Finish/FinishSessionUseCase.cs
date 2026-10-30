using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Finish;

/// <summary>
/// FinishSession (§6.20): ends custody with no incoming person. Requires the submitted, counted session and no
/// unresolved offer; the next start acknowledges its own opening. No synthetic incoming user is created.
/// </summary>
public class FinishSessionUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader,
    IBranchClock branchClock)
{
    private const string IdempotencyEndpoint = "POST /session/{id}/finish";

    public async Task<ResponseDailyCloseJson> Execute(
        Guid sessionId,
        RequestFinishSessionJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        SessionStarter.EnsureReasonLength(request.Reason);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);

        return await commandRunner.Run<object, ResponseDailyCloseJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            new { SessionId = sessionId, ExpectedVersion = expectedVersion, Request = request },
            [sessionKey.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var session = await dailyClosesRepository.GetByIdAndBranchId(sessionId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
                memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, session.AccountId);

                SessionCustodyRules.EnsureActiveCustody(session);
                var managerException = SessionCustodyRules.EnsureCustodianOrManagerException(
                    session,
                    branchUser,
                    memberScope,
                    request.Reason);
                if (session.Version != expectedVersion)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);
                if (session.Status != DailyCloseStatus.Submitted)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_FINISH_REQUIRES_SUBMITTED);
                if (await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(branchUser.BranchId, session.Id, ct) is not null)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_FINISH_PENDING_HANDOVER);

                var now = branchClock.UtcNow();
                session.CustodyState = SessionCustodyState.Finished;
                session.EndedAt = now;
                session.UpdatedAt = now;
                session.UpdatedByUserId = branchUser.UserId;
                await eventRecorder.Record(
                    session,
                    SessionEventKind.SessionFinished,
                    branchUser.User,
                    now,
                    new { EndedAt = now, ManagerException = managerException },
                    string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                    ct: ct);

                return async () => (session.Id, await responseLoader.Load(session.Id, branchUser.BranchId, ct));
            },
            ct);
    }
}
