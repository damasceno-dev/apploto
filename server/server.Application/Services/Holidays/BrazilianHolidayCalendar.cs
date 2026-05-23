using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.Services.Holidays;

public sealed class BrazilianHolidayCalendar : IBrazilianHolidayCalendar
{
    private const int MinimumYear = 1900;
    private const int MaximumYear = 2200;

    public IReadOnlyList<BrazilianHolidayEntry> GetForYear(int year, bool includeOptionalFederal)
    {
        if (year is < MinimumYear or > MaximumYear)
            throw new ArgumentOutOfRangeException(
                nameof(year),
                year,
                ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE);

        return BrazilianHolidayConceptCatalog.All
            .Where(concept => includeOptionalFederal || concept.Type == BrazilianHolidayType.National)
            .Select(concept => new BrazilianHolidayEntry(
                concept.ExpectedDateForYear(year),
                concept.CanonicalDescription,
                concept.Type))
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Type)
            .ToList();
    }
}
