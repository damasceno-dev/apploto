using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.DailyCloses;

public class DailyCloseWorkflowGuard(IBranchClock branchClock) : IDailyCloseWorkflowGuard
{
    public void EnsureCanOpen(BranchUser caller, Operator? callerOperator, DateTime branchLocalDate)
    {
        var today = branchClock.LocalBusinessDate(branchClock.UtcNow());

        if (branchLocalDate.Date > today)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_FUTURE_DATE_NOT_ALLOWED);

        if (caller.Role is Role.Manager or Role.Admin)
        {
            return;
        }

        // Account scope and duplicate-close races live outside this guard:
        // MemberAccountScopeGuard checks the request account before this call, and the
        // database unique constraint is the final authority for concurrent opens.
        if (callerOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        if (branchLocalDate.Date != today)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_MEMBER_OPEN_REQUIRES_TODAY);
    }

    public DailyCloseEditItemsOutcome EnsureCanEditItems(
        DailyClose close,
        BranchUser caller,
        Operator? callerOperator)
    {
        switch (close.Status)
        {
            case DailyCloseStatus.Approved:
            case DailyCloseStatus.Submitted:
                throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
            case DailyCloseStatus.Draft:
            case DailyCloseStatus.Rejected:
                break;
            default:
                throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        }

        // Draft or Rejected — Manager/Admin always allowed.
        if (caller.Role is Role.Manager or Role.Admin)
        {
            return close.Status == DailyCloseStatus.Rejected
                ? DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft
                : DailyCloseEditItemsOutcome.EditOnDraft;
        }

        if (callerOperator is null)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

        // An uncounted close has no recorder yet. Account scope has already been proven by the
        // caller, and LockDateGuard runs immediately after this guard, so any scoped Member may
        // make the first successful count even on a back-dated elevated-opened close. Once the
        // coordinated save commits, the recorder is immutable and the ordinary/correction window
        // applies. Submit and Recall deliberately do not have this unclaimed branch.
        var isUnclaimed = close.RecordedByUserId is null;
        var isRecorder = close.RecordedByOperatorId == callerOperator.Id;
        var windowOpen = close.RejectionReason is not null
            || close.OpeningRecheckRequiredAt is not null
            || branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow());
        var memberCanEdit = isUnclaimed || (isRecorder && windowOpen);

        if (memberCanEdit is false)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);

        return close.Status == DailyCloseStatus.Rejected
            ? DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft
            : DailyCloseEditItemsOutcome.EditOnDraft;
    }

    public void EnsureCanSubmit(DailyClose close, BranchUser caller, Operator? callerOperator)
    {
        if (close.Status is not (DailyCloseStatus.Draft or DailyCloseStatus.Rejected))
        {
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
        }

        if (caller.Role is Role.Manager or Role.Admin)
        {
            return;
        }

        if (callerOperator is null)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        }

        if (callerOperator.Id != close.RecordedByOperatorId)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        }

        if (close.RejectionReason is null
            && close.OpeningRecheckRequiredAt is null
            && branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow()) is false)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        }
    }

    public void EnsureCanApprove(DailyClose close, BranchUser caller)
    {
        if (caller.Role is not (Role.Manager or Role.Admin))
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        if (close.Status != DailyCloseStatus.Submitted)
        {
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE);
        }
    }

    public void EnsureCanReject(DailyClose close, BranchUser caller)
    {
        if (caller.Role is not (Role.Manager or Role.Admin))
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        }

        if (close.Status != DailyCloseStatus.Submitted)
        {
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_REJECTABLE);
        }
    }

    public void EnsureCanRecall(DailyClose close, BranchUser caller, Operator? callerOperator)
    {
        if (close.Status != DailyCloseStatus.Submitted)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_RECALLABLE);

        if (caller.Role is Role.Manager or Role.Admin)
            return;

        if (callerOperator is null)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);

        if (callerOperator.Id != close.RecordedByOperatorId)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);

        if (branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow()) is false)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.DAILYCLOSE_RECALL_REQUIRES_SAME_DAY);
    }

    public void EnsureCanReopen(DailyClose close, BranchUser caller)
    {
        if (caller.Role is not (Role.Manager or Role.Admin))
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        if (close.Status != DailyCloseStatus.Approved)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_REOPENABLE);
    }
}
