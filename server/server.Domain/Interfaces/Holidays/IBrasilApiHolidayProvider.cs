using server.Domain.Models;

namespace server.Domain.Interfaces.Holidays;

public interface IBrasilApiHolidayProvider
{
    Task<BrazilianHolidayProviderResult<IReadOnlyList<BrasilApiHolidayDto>>> GetHolidaysForYear(int year,CancellationToken cancellationToken);
}
