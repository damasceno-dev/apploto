namespace server.Application.Services.Holidays;

public interface IBranchHolidaySource
{
    Task<IReadOnlySet<DateOnly>> GetHolidayDatesAsync(Guid branchId, CancellationToken ct = default);
}
