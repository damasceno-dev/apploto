using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

/// <summary>
/// Computes TotalHours and BalanceHours for time entries based on status, clocks, and branch settings.
/// Two modes: <see cref="Calculate"/> for completed entries, <see cref="CalculateLiveRunning"/> for
/// in-progress Present entries using the current branch-local time as a live proxy.
/// </summary>
public class TimeEntryCalculationService : ITimeEntryCalculationService
{
    /// <summary>
    /// Computes persisted TotalHours and BalanceHours for a completed time entry.
    /// Present delegates to clock arithmetic; abonado statuses (Sunday, Holiday, Vacation,
    /// JustifiedAbsence) credit the full daily target; owing statuses (DayOff, UnjustifiedAbsence)
    /// return zero hours with negative balance.
    /// </summary>
    /// <param name="status">Entry status determining the calculation branch.</param>
    /// <param name="clockIn">Clock-in time. Required when Present.</param>
    /// <param name="clockOut">Clock-out time. Required when Present.</param>
    /// <param name="dailyTargetHours">Branch daily work target in fractional hours.</param>
    /// <param name="lunchDeductionOver6H">Lunch deduction when gross hours exceed 6.</param>
    /// <param name="lunchDeductionOver4H">Lunch deduction when gross hours exceed 4 but not 6.</param>
    /// <returns>(TotalHours, BalanceHours) where BalanceHours = TotalHours - dailyTargetHours.</returns>
    public (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        TimeOnly? clockIn,
        TimeOnly? clockOut,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H)
    {
        return status switch
        {
            TimeEntryStatus.Present => CalculatePresent(clockIn!.Value, clockOut!.Value, dailyTargetHours, lunchDeductionOver6H, lunchDeductionOver4H),
            TimeEntryStatus.Sunday or TimeEntryStatus.Holiday or TimeEntryStatus.Vacation or TimeEntryStatus.JustifiedAbsence => (dailyTargetHours, 0m),
            _ => (0m, -dailyTargetHours)
        };

        // DayOff or UnjustifiedAbsence — hours owed
    }

    /// <summary>
    /// Live-running estimate for an in-progress Present entry (no ClockOut yet).
    /// Uses branchLocalNow as the effective clock-out.
    /// ClockOut less than ClockIn:
    ///     Same day: 
    /// </summary>
    /// <param name="clockIn">Recorded clock-in time.</param>
    /// <param name="entryDate">Calendar date the entry belongs to.</param>
    /// <param name="branchLocalNow">Current branch-local date and time from IBranchClock.</param>
    /// <param name="dailyTargetHours">Branch daily work target in fractional hours.</param>
    /// <param name="lunchDeductionOver6H">Lunch deduction when gross hours exceed 6.</param>
    /// <param name="lunchDeductionOver4H">Lunch deduction when gross hours exceed 4 but not 6.</param>
    /// <returns>(TotalHours, BalanceHours), or (0, 0) / (0, -dailyTargetHours) for ineligible entries.</returns>
    public (decimal TotalHours, decimal BalanceHours) CalculateLiveRunning(
        TimeOnly clockIn,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H)
    {
        if (branchLocalNow.Date != entryDate.Date) //forgotten clock-out from a prior day
            return (0m, 0m); 

        var effectiveClockOut = TimeOnly.FromDateTime(branchLocalNow);

        return effectiveClockOut < clockIn ? 
            (0m, -dailyTargetHours) : //same day but clockOut < clockIn: something is wrong
            CalculatePresent(clockIn, effectiveClockOut, dailyTargetHours, lunchDeductionOver6H, lunchDeductionOver4H);  //normal path, calculate considering lunch tiers
    }

    /// <summary>
    /// Core Present arithmetic: gross elapsed hours minus tiered lunch deduction minus daily target.
    /// Supports overnight shifts via 24h wrap when clockOut less than clockIn.
    /// </summary>
    /// <param name="clockIn">Start of the shift.</param>
    /// <param name="clockOut">End of the shift. May be before clockIn for overnight shifts.</param>
    /// <param name="dailyTargetHours">Branch daily work target in fractional hours.</param>
    /// <param name="lunchDeductionOver6H">Lunch deduction when gross hours exceed 6.</param>
    /// <param name="lunchDeductionOver4H">Lunch deduction when gross hours exceed 4 but not 6.</param>
    /// <returns>(TotalHours, BalanceHours) where TotalHours = gross - lunch, BalanceHours = TotalHours - target.</returns>
    private static (decimal TotalHours, decimal BalanceHours) CalculatePresent(
        TimeOnly clockIn,
        TimeOnly clockOut,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H)
    {
        var span = clockOut.ToTimeSpan() - clockIn.ToTimeSpan();
        if (span < TimeSpan.Zero)
            span = span.Add(TimeSpan.FromHours(24));

        var grossHours = (decimal)span.TotalMinutes / 60m;

        var lunchDeduction = grossHours > 6m ? lunchDeductionOver6H
            : grossHours > 4m ? lunchDeductionOver4H
            : 0m;

        var totalHours = grossHours - lunchDeduction;
        var balanceHours = totalHours - dailyTargetHours;

        return (totalHours, balanceHours);
    }
}
