namespace server.Application.Services.TimeEntries;

/// <summary>
/// Permission classification returned by <see cref="ITimeEntryWritePermissionGuard"/>.
/// </summary>
public enum TimeEntryWritePermissionOutcome
{
    /// <summary>
    /// The caller is a Member writing their linked operator's Present entry for the current branch-local business day.
    /// </summary>
    SelfSameDayPresent,

    /// <summary>
    /// The caller is an Admin or Manager and may write time entries for any operator in the branch.
    /// </summary>
    Elevated,

    /// <summary>
    /// The caller is a Member but has no operator linked to their user in this branch.
    /// </summary>
    MissingLinkedOperator,

    /// <summary>
    /// The caller is a Member and is trying to write a time entry for another operator.
    /// </summary>
    NotOwnOperator,

    /// <summary>
    /// The caller is a Member writing their own operator, but the target date is not the current branch-local business day.
    /// </summary>
    OlderDayMember,

    /// <summary>
    /// The caller is a Member writing their own same-day entry, but the requested status is not Present.
    /// </summary>
    MemberNonPresent
}
