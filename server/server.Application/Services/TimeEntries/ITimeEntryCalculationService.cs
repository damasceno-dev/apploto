using server.Domain.Entities.Enums;

namespace server.Application.Services.TimeEntries;

public interface ITimeEntryCalculationService
{
    (decimal TotalHours, decimal BalanceHours) Calculate(
        TimeEntryStatus status,
        TimeOnly? clockIn,
        TimeOnly? clockOut,
        decimal dailyTargetHours,
        decimal lunchDeductionOver6H,
        decimal lunchDeductionOver4H);
}
