using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

/// <summary>
/// Computes TotalHours and BalanceHours for time entries based on status, clocks, and branch settings.
/// </summary>
public interface ITimeEntryCalculationService
{
    /// <summary>
    /// Computes persisted TotalHours and BalanceHours for a completed time entry.
    /// </summary>
    (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        TimeOnly? clockIn,
        TimeOnly? clockOut,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H);

    /// <summary>
    /// Live-running estimate for an in-progress Present entry (no ClockOut yet).
    /// Returns (0, 0) for prior-day forgotten clock-outs, (0, -dailyTargetHours) for clock drift.
    /// </summary>
    (decimal TotalHours, decimal BalanceHours) CalculateLiveRunning(
        TimeOnly clockIn,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H);
}
