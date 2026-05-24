using server.Domain.Models;

namespace server.Domain.Interfaces.Holidays;

public interface INagerDateHolidayProvider
{
    Task<BrazilianHolidayProviderResult<IReadOnlyList<NagerDateHolidayDto>>> GetHolidaysForYear(int year,CancellationToken cancellationToken);
}