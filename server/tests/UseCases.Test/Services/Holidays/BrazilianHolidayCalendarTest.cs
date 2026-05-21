using server.Application.Services.Holidays;
using server.Domain.Entities.Enums;
using Shouldly;
using static server.Application.Services.Holidays.BrazilianHolidayEntry;
using Xunit;

namespace UseCases.Test.Services.Holidays;

public class BrazilianHolidayCalendarTest
{
    private static readonly BrazilianHolidayCalendar Calendar = new();

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2025()
    {
        AssertFullSnapshot(2025, [
            National(2025, 1, 1, "Confraternização Universal"),
            OptionalFederal(2025, 3, 4, "Carnaval (terça)"),
            OptionalFederal(2025, 3, 5, "Quarta-feira de Cinzas (até 14h)"),
            National(2025, 4, 18, "Sexta-feira Santa"),
            National(2025, 4, 21, "Tiradentes"),
            National(2025, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2025, 6, 19, "Corpus Christi"),
            National(2025, 9, 7, "Independência"),
            National(2025, 10, 12, "Nossa Senhora Aparecida"),
            National(2025, 11, 2, "Finados"),
            National(2025, 11, 15, "Proclamação da República"),
            National(2025, 11, 20, "Consciência Negra"),
            National(2025, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2026()
    {
        AssertFullSnapshot(2026, [
            National(2026, 1, 1, "Confraternização Universal"),
            OptionalFederal(2026, 2, 17, "Carnaval (terça)"),
            OptionalFederal(2026, 2, 18, "Quarta-feira de Cinzas (até 14h)"),
            National(2026, 4, 3, "Sexta-feira Santa"),
            National(2026, 4, 21, "Tiradentes"),
            National(2026, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2026, 6, 4, "Corpus Christi"),
            National(2026, 9, 7, "Independência"),
            National(2026, 10, 12, "Nossa Senhora Aparecida"),
            National(2026, 11, 2, "Finados"),
            National(2026, 11, 15, "Proclamação da República"),
            National(2026, 11, 20, "Consciência Negra"),
            National(2026, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2027()
    {
        AssertFullSnapshot(2027, [
            National(2027, 1, 1, "Confraternização Universal"),
            OptionalFederal(2027, 2, 9, "Carnaval (terça)"),
            OptionalFederal(2027, 2, 10, "Quarta-feira de Cinzas (até 14h)"),
            National(2027, 3, 26, "Sexta-feira Santa"),
            National(2027, 4, 21, "Tiradentes"),
            National(2027, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2027, 5, 27, "Corpus Christi"),
            National(2027, 9, 7, "Independência"),
            National(2027, 10, 12, "Nossa Senhora Aparecida"),
            National(2027, 11, 2, "Finados"),
            National(2027, 11, 15, "Proclamação da República"),
            National(2027, 11, 20, "Consciência Negra"),
            National(2027, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2028()
    {
        AssertFullSnapshot(2028, [
            National(2028, 1, 1, "Confraternização Universal"),
            OptionalFederal(2028, 2, 29, "Carnaval (terça)"),
            OptionalFederal(2028, 3, 1, "Quarta-feira de Cinzas (até 14h)"),
            National(2028, 4, 14, "Sexta-feira Santa"),
            National(2028, 4, 21, "Tiradentes"),
            National(2028, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2028, 6, 15, "Corpus Christi"),
            National(2028, 9, 7, "Independência"),
            National(2028, 10, 12, "Nossa Senhora Aparecida"),
            National(2028, 11, 2, "Finados"),
            National(2028, 11, 15, "Proclamação da República"),
            National(2028, 11, 20, "Consciência Negra"),
            National(2028, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2029()
    {
        AssertFullSnapshot(2029, [
            National(2029, 1, 1, "Confraternização Universal"),
            OptionalFederal(2029, 2, 13, "Carnaval (terça)"),
            OptionalFederal(2029, 2, 14, "Quarta-feira de Cinzas (até 14h)"),
            National(2029, 3, 30, "Sexta-feira Santa"),
            National(2029, 4, 21, "Tiradentes"),
            National(2029, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2029, 5, 31, "Corpus Christi"),
            National(2029, 9, 7, "Independência"),
            National(2029, 10, 12, "Nossa Senhora Aparecida"),
            National(2029, 11, 2, "Finados"),
            National(2029, 11, 15, "Proclamação da República"),
            National(2029, 11, 20, "Consciência Negra"),
            National(2029, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnFullSnapshot_For2030()
    {
        AssertFullSnapshot(2030, [
            National(2030, 1, 1, "Confraternização Universal"),
            OptionalFederal(2030, 3, 5, "Carnaval (terça)"),
            OptionalFederal(2030, 3, 6, "Quarta-feira de Cinzas (até 14h)"),
            National(2030, 4, 19, "Sexta-feira Santa"),
            National(2030, 4, 21, "Tiradentes"),
            National(2030, 5, 1, "Dia do Trabalho"),
            OptionalFederal(2030, 6, 20, "Corpus Christi"),
            National(2030, 9, 7, "Independência"),
            National(2030, 10, 12, "Nossa Senhora Aparecida"),
            National(2030, 11, 2, "Finados"),
            National(2030, 11, 15, "Proclamação da República"),
            National(2030, 11, 20, "Consciência Negra"),
            National(2030, 12, 25, "Natal")
        ]);
    }

    [Fact]
    public void GetForYear_ShouldReturnExpectedCounts_ByOptionalFlag()
    {
        Calendar.GetForYear(2026, includeOptionalFederal: false).Count.ShouldBe(10);
        Calendar.GetForYear(2026, includeOptionalFederal: true).Count.ShouldBe(13);
    }

    [Fact]
    public void GetForYear_ShouldReturnEntriesInAscendingDateOrder_InBothModes()
    {
        AssertAscending(Calendar.GetForYear(2028, includeOptionalFederal: false));
        AssertAscending(Calendar.GetForYear(2028, includeOptionalFederal: true));
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2201)]
    public void GetForYear_ShouldThrowArgumentOutOfRangeException_WhenYearIsOutsideRange(int year)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => Calendar.GetForYear(year, includeOptionalFederal: true));
    }

    [Fact]
    public void GetForYear_ShouldKeepNationalEntriesAsStrictSubsetAndOptionalEntriesAsEasterAnchoredSet()
    {
        var nationalOnly = Calendar.GetForYear(2029, includeOptionalFederal: false);
        var full = Calendar.GetForYear(2029, includeOptionalFederal: true);

        full.Where(entry => entry.Type == BrazilianHolidayType.National).ShouldBe(nationalOnly);
        full.Count.ShouldBeGreaterThan(nationalOnly.Count);

        var optionals = full.Where(entry => entry.Type == BrazilianHolidayType.OptionalFederal).ToList();
        optionals.ShouldBe([
            OptionalFederal(2029, 2, 13, "Carnaval (terça)"),
            OptionalFederal(2029, 2, 14, "Quarta-feira de Cinzas (até 14h)"),
            OptionalFederal(2029, 5, 31, "Corpus Christi")
        ]);
    }

    private void AssertFullSnapshot(int year, IReadOnlyList<BrazilianHolidayEntry> expected)
    {
        // Snapshot dates were cross-checked against https://www.timeanddate.com/holidays/brazil/{year}
        // for 2025-2030 and the fixed federal-law holiday list documented in loto-specs §5.1.
        var actual = Calendar.GetForYear(year, includeOptionalFederal: true);

        actual.ShouldBe(expected);
    }

    private static void AssertAscending(IReadOnlyList<BrazilianHolidayEntry> entries)
    {
        entries.ShouldBe(entries.OrderBy(entry => entry.Date).ThenBy(entry => entry.Type).ToList());
    }
}
