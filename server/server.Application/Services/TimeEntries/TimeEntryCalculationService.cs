using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

/// <summary>
/// Per-day TotalHours / BalanceHours engine for time entries (loto-specs §6.7). This is
/// the single source of truth for the calculation: read paths re-run it on every request
/// so live-running (open current-day) entries reflect "now" rather than the persisted
/// checkpoint. Pure and I/O-free — every input, including the caller-captured
/// <paramref name="branchLocalNow"/>, is passed in.
/// </summary>
public class TimeEntryCalculationService : ITimeEntryCalculationService
{
    public (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        IReadOnlyList<TimeEntrySegmentInput> segments,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H)
    {
        // Status drives the whole model (§6.7). Only Present is computed from the actual
        // segments; every other status resolves to a fixed day-level constant.
        return status switch
        {
            TimeEntryStatus.Present => CalculatePresent(
                segments,
                entryDate,
                branchLocalNow,
                dailyTargetHours,
                lunchDeductionOver6H,
                lunchDeductionOver4H),

            // Abonado (credited) days: counted as a full worked target, so the day is
            // balance-neutral (TotalHours = target, Balance = 0). Segments are not used.
            TimeEntryStatus.Sunday or TimeEntryStatus.Holiday or TimeEntryStatus.Vacation or TimeEntryStatus.JustifiedAbsence => (dailyTargetHours, 0m),

            // Owing days (DayOff, UnjustifiedAbsence): nothing worked, so the operator
            // owes a full target — TotalHours = 0, Balance = -target.
            _ => (0m, -dailyTargetHours)
        };
    }

    private static (decimal TotalHours, decimal BalanceHours) CalculatePresent(
        IReadOnlyList<TimeEntrySegmentInput> segments,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H)
    {
        // A Present day with no segments means the shift was never logged: nothing
        // worked, owes the full target.
        if (segments.Count is 0)
            return (0m, -dailyTargetHours);

        // Segments may arrive in any order; the gap math below relies on chronological order.
        var orderedSegments = segments
            .OrderBy(segment => segment.ClockIn)
            .ToList();

        // grossHours: worked time before the lunch deduction.
        // totalGapHours: breaks already taken between segments — used to offset the
        // automatic lunch deduction so a real break isn't deducted twice.
        var grossHours = 0m;
        var totalGapHours = 0m;

        for (var index = 0; index < orderedSegments.Count; index++)
        {
            var segment = orderedSegments[index];
            if (segment.ClockOut is { } clockOut)
            {
                // Closed segment contributes its full duration. DateTime subtraction is
                // exact, so overnight spans need no midnight wrap-around handling.
                grossHours += ToHours(clockOut - segment.ClockIn);

                // Gap to the next segment (open or closed). Per §6.7 a gap is attributed
                // to the *preceding* closed segment, so it is added here — when the closed
                // segment is visited — not when the following segment is.
                if (index + 1 < orderedSegments.Count)
                    totalGapHours += ToHours(orderedSegments[index + 1].ClockIn - clockOut);

                continue;
            }

            // Open segment (ClockOut == null): the operator is, or was, still clocked in.

            // Forgotten clock-out on a past day contributes 0 h and is left for manual
            // review (§6.7, Example 6). Only a *current-day* open segment runs live.
            if (entryDate.Date != branchLocalNow.Date)
                continue;

            // Degenerate live state — "now" has not advanced past the open clock-in
            // (clock skew / bad data). There is no positive contribution to add, so treat
            // the day as not yet worked rather than producing negative time.
            if (branchLocalNow <= segment.ClockIn)
                return (0m, -dailyTargetHours);

            // Live contribution of the running shift: elapsed time since clock-in
            // (§6.7, Example 7).
            grossHours += ToHours(branchLocalNow - segment.ClockIn);
        }

        // Automatic lunch deduction tier, strictly greater-than (§6.7): > 6 h gross uses
        // the larger deduction, > 4 h the smaller, <= 4 h none. Boundaries: exactly 4 h -> 0,
        // exactly 6 h -> the over-4 h tier.
        var lunchTier = grossHours > 6m ? lunchDeductionOver6H
            : grossHours > 4m ? lunchDeductionOver4H
            : 0m;

        // Breaks already taken (the gaps) count toward lunch, so subtract them from the
        // automatic deduction — floored at zero so large gaps never *add* paid time.
        var effectiveLunch = Math.Max(0m, lunchTier - totalGapHours);
        var totalHours = grossHours - effectiveLunch;
        var balanceHours = totalHours - dailyTargetHours;

        return (totalHours, balanceHours);
    }

    // Exact decimal hours from a span (minutes / 60). Decimal keeps payroll math base-10
    // exact; DateTime subtraction is already exact, so there is no wrap-around to handle.
    private static decimal ToHours(TimeSpan value) => (decimal)value.TotalMinutes / 60m;
}
