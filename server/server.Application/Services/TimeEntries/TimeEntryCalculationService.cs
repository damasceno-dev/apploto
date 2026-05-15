using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

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
        return status switch
        {
            TimeEntryStatus.Present => CalculatePresent(
                segments,
                entryDate,
                branchLocalNow,
                dailyTargetHours,
                lunchDeductionOver6H,
                lunchDeductionOver4H),
            TimeEntryStatus.Sunday or TimeEntryStatus.Holiday or TimeEntryStatus.Vacation or TimeEntryStatus.JustifiedAbsence => (dailyTargetHours, 0m),
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
        if (segments.Count is 0)
            return (0m, -dailyTargetHours);

        var orderedSegments = segments
            .OrderBy(segment => segment.ClockIn)
            .ToList();

        var grossHours = 0m;
        var totalGapHours = 0m;

        for (var index = 0; index < orderedSegments.Count; index++)
        {
            var segment = orderedSegments[index];
            if (segment.ClockOut is { } clockOut)
            {
                grossHours += ToHours(clockOut - segment.ClockIn);

                if (index + 1 < orderedSegments.Count)
                    totalGapHours += ToHours(orderedSegments[index + 1].ClockIn - clockOut);

                continue;
            }

            if (entryDate.Date != branchLocalNow.Date)
                continue;

            if (branchLocalNow <= segment.ClockIn)
                return (0m, -dailyTargetHours);

            grossHours += ToHours(branchLocalNow - segment.ClockIn);
        }

        var lunchTier = grossHours > 6m ? lunchDeductionOver6H
            : grossHours > 4m ? lunchDeductionOver4H
            : 0m;

        var effectiveLunch = Math.Max(0m, lunchTier - totalGapHours);
        var totalHours = grossHours - effectiveLunch;
        var balanceHours = totalHours - dailyTargetHours;

        return (totalHours, balanceHours);
    }

    private static decimal ToHours(TimeSpan value) => (decimal)value.TotalMinutes / 60m;
}
