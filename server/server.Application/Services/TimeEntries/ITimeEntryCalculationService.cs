using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

public readonly record struct TimeEntrySegmentInput(DateTime ClockIn, DateTime? ClockOut);

/// <summary>
/// Computes TotalHours and BalanceHours for time entries based on status, segments, and branch settings.
/// </summary>
public interface ITimeEntryCalculationService
{
    (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        IReadOnlyList<TimeEntrySegmentInput> segments,
        DateTime entryDate,
        DateTime branchLocalNow,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H);
}
