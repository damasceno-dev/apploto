using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.Services.TimeEntries;

public sealed class TimeEntryWritePermissionGuard(IBranchClock branchClock) : ITimeEntryWritePermissionGuard
{
    /// <summary>
    /// Classifies whether the branch user may write a time entry for the requested operator, date, and status.
    /// </summary>
    /// <remarks>
    /// Admin and Manager are elevated branch roles, so they may write any operator's entry in the branch
    /// for any date or status. Members are limited to the mobile self-entry path: they must have a linked
    /// operator, target that same operator, write only for the current branch-local business day, and submit
    /// only <see cref="TimeEntryStatus.Present"/>.
    ///
    /// The check order is intentional because the returned outcome is mapped to the user-facing failure:
    /// missing operator link beats ownership checks, ownership beats date checks, and an old date beats
    /// a non-Present status.
    /// </remarks>
    public TimeEntryWritePermissionOutcome Evaluate(
        BranchUser caller,
        Operator? callerOperator,
        Guid targetOperatorId,
        DateTime targetDate,
        TimeEntryStatus status,
        DateTime utcNow)
    {
        if (caller.Role is Role.Manager or Role.Admin)
            return TimeEntryWritePermissionOutcome.Elevated;

        if (callerOperator is null)
            return TimeEntryWritePermissionOutcome.MissingLinkedOperator;

        if (callerOperator.Id != targetOperatorId)
            return TimeEntryWritePermissionOutcome.NotOwnOperator;

        if (branchClock.IsSameLocalDay(targetDate, utcNow) is false)
            return TimeEntryWritePermissionOutcome.OlderDayMember;

        // Past this point, the caller is a linked Member writing their own same-day entry.
        // The only remaining Member restriction is that self-entry may submit Present only.
        return status is not TimeEntryStatus.Present ?
            TimeEntryWritePermissionOutcome.MemberNonPresent :
            TimeEntryWritePermissionOutcome.SelfSameDayPresent;
    }
}
