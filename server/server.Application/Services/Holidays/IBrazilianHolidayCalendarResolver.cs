using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

public interface IBrazilianHolidayCalendarResolver
{
    Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> GetForYear(int year,bool includeOptionalFederal,BrazilianHolidayCalendarSource source,CancellationToken cancellationToken);
}
