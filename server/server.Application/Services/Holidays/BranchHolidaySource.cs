using server.Domain.Interfaces;

namespace server.Application.Services.Holidays;

/// <summary>
/// Small application-layer adapter around IHolidaysRepository to avoid spreading holiday logic throughout the application.
/// </summary>
public class BranchHolidaySource(IHolidaysRepository holidaysRepository) : IBranchHolidaySource
{
    public async Task<IReadOnlySet<DateOnly>> GetHolidayDatesAsync(Guid branchId, CancellationToken ct = default)
    {
        var dates = await holidaysRepository.ListActiveDatesByBranchIdAsNoTracking(branchId);
        return dates.ToHashSet();
    }
}
