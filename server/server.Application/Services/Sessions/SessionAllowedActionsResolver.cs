using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Sessions;

/// <summary>
/// The caller's permitted actions on a session, derived from the same guards the commands run (current membership,
/// grants, custodian, recorder windows, lock date, candidate state and separation of duties). Read-only: a projection,
/// not a promise that a later write succeeds.
/// </summary>
public sealed class SessionAllowedActionsResolver(
    IDailyCloseWorkflowGuard workflowGuard,
    ILockDateReader lockDateReader,
    IBranchUsersRepository branchUsersRepository)
{
    public async Task<ResponseSessionAllowedActionsJson> Resolve(
        DailyClose session,
        BranchUser caller,
        MemberAccountScope scope,
        SessionCandidateState candidateState,
        bool hasAttempt,
        CancellationToken ct = default)
    {
        var isManager = caller.Role is Role.Manager or Role.Admin;
        var inScope = isManager || (scope.LinkedOperator is not null && scope.AllowedAccountIds.Contains(session.AccountId));
        var lockDate = await lockDateReader.Read(caller.BranchId, ct);
        var unlocked = session.Date.Date > lockDate.Date;
        var writable = inScope && unlocked && session.MappingState == SessionMappingState.Ready;

        var canSaveCount = writable && Allows(() => workflowGuard.EnsureCanEditItems(session, caller, scope.LinkedOperator));
        var isDraft = session.Status is DailyCloseStatus.Draft or DailyCloseStatus.Rejected;
        var canConferir = canSaveCount
                          && isDraft
                          && session.ItemsFirstRecordedAt is not null
                          && candidateState != SessionCandidateState.Current;
        var canRecount = canSaveCount && isDraft && hasAttempt && candidateState != SessionCandidateState.Recounting;
        var canSubmit = writable
                        && candidateState == SessionCandidateState.Current
                        && Allows(() => workflowGuard.EnsureCanSubmit(session, caller, scope.LinkedOperator));
        var canRecall = writable && Allows(() => workflowGuard.EnsureCanRecall(session, caller, scope.LinkedOperator));

        var approvable = writable && Allows(() => workflowGuard.EnsureCanApprove(session, caller));
        var conflict = approvable ? SessionApprovalPolicy.DutyConflict(session, caller.UserId, scope.LinkedOperator) : null;
        var singleManager = approvable
                            && conflict is not null
                            && await branchUsersRepository.CountActiveManagersByBranchIdAsNoTracking(caller.BranchId, ct) == 1;

        return new ResponseSessionAllowedActionsJson
        {
            CanSaveCount = canSaveCount,
            CanConferir = canConferir,
            CanRecount = canRecount,
            CanSubmit = canSubmit,
            CanRecall = canRecall,
            CanApprove = approvable && conflict is null,
            CanApproveAsSingleManager = singleManager,
            ApprovalBlockedReason = conflict,
            CanReject = writable && Allows(() => workflowGuard.EnsureCanReject(session, caller)),
            CanReopen = writable && Allows(() => workflowGuard.EnsureCanReopen(session, caller))
        };
    }

    private static bool Allows(Action guard)
    {
        try
        {
            guard();
            return true;
        }
        catch (ServerException)
        {
            return false;
        }
    }
}
