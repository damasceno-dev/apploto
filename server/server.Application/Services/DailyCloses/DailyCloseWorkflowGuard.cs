using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.DailyCloses;

public class DailyCloseWorkflowGuard(IBranchClock branchClock) : IDailyCloseWorkflowGuard
{
    public void EnsureCanOpen(BranchUser caller, Operator? callerOperator, Guid accountId, DateTime branchLocalDate)
    {
    }

    public DailyCloseEditItemsOutcome EnsureCanEditItems(
        DailyClose close,
        BranchUser caller,
        Operator? callerOperator)
    {
        switch (close.Status)
        {
            case DailyCloseStatus.Approved:
                throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
            case DailyCloseStatus.Submitted:
            {
                var recallAllowed =
                    caller.Role is Role.Manager or Role.Admin
                    || (callerOperator is not null
                        && callerOperator.Id == close.SubmittedByOperatorId
                        && branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow()));

                return recallAllowed is false ? 
                    throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE) : 
                    DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft;
            }
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

        // Member must be the recording operator AND on the same local business day.
        var memberCanEdit = callerOperator is not null
            && callerOperator.Id == close.SubmittedByOperatorId
            && branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow());

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

        if (callerOperator.Id != close.SubmittedByOperatorId)
        {
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        }

        if (branchClock.IsSameLocalDay(close.Date, branchClock.UtcNow()) is false)
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
}
