using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

public class TimeEntryCalculationService : ITimeEntryCalculationService
{
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
