using server.Exceptions;
using static server.Application.Services.Holidays.BrazilianHolidayEntry;

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

        var easter = CalculateGregorianEaster(year);

        var entries = new List<BrazilianHolidayEntry>
        {
            National(year, 1, 1, "Confraternização Universal"),
            National(easter.AddDays(-2), "Sexta-feira Santa"),
            National(year, 4, 21, "Tiradentes"),
            National(year, 5, 1, "Dia do Trabalho"),
            National(year, 9, 7, "Independência"),
            National(year, 10, 12, "Nossa Senhora Aparecida"),
            National(year, 11, 2, "Finados"),
            National(year, 11, 15, "Proclamação da República"),
            National(year, 11, 20, "Consciência Negra"),
            National(year, 12, 25, "Natal")
        };

        if (includeOptionalFederal)
        {
            entries.AddRange([
                OptionalFederal(easter.AddDays(-47), "Carnaval (terça)"),
                OptionalFederal(easter.AddDays(-46), "Quarta-feira de Cinzas (até 14h)"),
                OptionalFederal(easter.AddDays(60), "Corpus Christi")
            ]);
        }

        return entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Type)
            .ToList();
    }

    private static DateOnly CalculateGregorianEaster(int year)
    {
        // Anonymous Gregorian algorithm, also known as the Meeus/Jones/Butcher algorithm.
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
