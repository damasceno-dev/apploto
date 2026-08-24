using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.TimeEntries;

public readonly record struct TimeEntrySegmentRuleInput(
    Guid Id,
    DateTime CreatedAt,
    DateTime ClockIn,
    DateTime? ClockOut);

public class TimeEntrySegmentMutationService(
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    ITimeEntryCalculationService calculationService)
{
    public static DateTime AsUnspecified(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    public static DateTime? AsUnspecified(DateTime? value)
    {
        return value.HasValue ? AsUnspecified(value.Value) : null;
    }

    public static List<TimeEntrySegment> ActiveSegments(TimeEntry entry)
    {
        return entry.Segments
            .Where(segment => segment.Active)
            .OrderBy(segment => segment.ClockIn)
            .ThenBy(segment => segment.CreatedAt)
            .ThenBy(segment => segment.Id)
            .ToList();
    }

    /// <summary>
    /// Projects the parent's active segments into immutable validation inputs so candidate edits can be checked without mutating tracked entities first.
    /// </summary>
    public static List<TimeEntrySegmentRuleInput> ActiveSegmentRuleInputs(TimeEntry entry)
    {
        return ActiveSegments(entry)
            .Select(ToRuleInput)
            .ToList();
    }

    /// <summary>
    /// Copies the fields needed by segment validation from a tracked entity into a lightweight rule input.
    /// </summary>
    public static TimeEntrySegmentRuleInput ToRuleInput(TimeEntrySegment segment)
    {
        return new TimeEntrySegmentRuleInput(
            segment.Id,
            segment.CreatedAt,
            segment.ClockIn,
            segment.ClockOut);
    }

    /// <summary>
    /// Ensures the caller is allowed to perform admin-style segment mutations.
    /// </summary>
    public static void EnsureElevated(BranchUser branchUser)
    {
        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    /// <summary>
    /// Ensures segment mutations only run on Present entries; non-Present statuses represent credited or owed days and must not carry worked clock segments.
    /// </summary>
    public static void EnsureParentAcceptsSegments(TimeEntry entry)
    {
        if (entry.Status is not TimeEntryStatus.Present)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS]);
    }

    /// <summary>
    /// Validates the full candidate active segment set for one TimeEntry: required ClockIn, day bounds, positive duration, max 24h span, at most one open segment, and no overlaps.
    /// </summary>
    public static void EnsureSegmentRules(DateTime entryDate, IEnumerable<TimeEntrySegmentRuleInput> candidateSegments)
    {
        var segments = candidateSegments
            .OrderBy(segment => segment.ClockIn)
            .ThenBy(segment => segment.CreatedAt)
            .ThenBy(segment => segment.Id)
            .ToList();
        var dayStart = entryDate.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var segment in segments)
        {
            if (segment.ClockIn == default)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED]);

            if (segment.ClockIn < dayStart || segment.ClockIn >= dayEnd)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);

            if (segment.ClockOut is not { } clockOut)
                continue;

            if (clockOut <= segment.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN]);

            if (clockOut - segment.ClockIn > TimeSpan.FromHours(24))
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);
        }

        if (segments.Count(segment => segment.ClockOut is null) > 1)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

        for (var index = 0; index < segments.Count - 1; index++)
        {
            var current = segments[index];
            var next = segments[index + 1];

            if (current.ClockOut is null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

            if (current.ClockOut > next.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP]);
        }
    }

    /// <summary>
    /// Resolves the effective-dated policy applicable to the entry's date, recalculates parent TotalHours and BalanceHours from active segments, and stamps parent audit fields with the caller and captured UTC instant.
    /// </summary>
    public async Task RecalculateParentTotalsAndStampAudit(
        TimeEntry entry,
        BranchUser branchUser,
        DateTime utcNow,
        DateTime branchLocalNow)
    {
        var policies = await timeEntryPoliciesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        var policy = TimeEntryPolicyResolver.Resolve(policies, entry.Date);

        var (totalHours, balanceHours) = calculationService.Calculate(
            entry.Status,
            ActiveSegments(entry)
                .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
                .ToList(),
            entry.Date,
            branchLocalNow,
            policy.DailyTargetHours,
            policy.LunchDeductionOver6H,
            policy.LunchDeductionOver4H);

        entry.TotalHours = totalHours;
        entry.BalanceHours = balanceHours;
        entry.UpdatedAt = utcNow;
        entry.UpdatedByUserId = branchUser.UserId;
    }
}
