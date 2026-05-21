namespace server.Application.Services.Holidays;

public interface IBrazilianHolidayCalendar
{
    IReadOnlyList<BrazilianHolidayEntry> GetForYear(int year, bool includeOptionalFederal);
}
