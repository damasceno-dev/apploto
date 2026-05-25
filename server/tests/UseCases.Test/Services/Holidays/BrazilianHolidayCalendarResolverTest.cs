using CommonTestUtilities.Services;
using Microsoft.Extensions.Logging.Abstractions;
using server.Application.Services.Holidays;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces.Holidays;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Holidays;

/// <summary>
/// End-to-end behaviors of <see cref="BrazilianHolidayCalendarResolver"/>:
/// per-source dispatch, Composite priority and failure swallowing, single-source
/// 502, and the catalog match algorithm (name + date-proximity tiebreaker, off-date
/// still matches, regional Nager rows dropped, unmatched provider rows dropped).
/// </summary>
public class BrazilianHolidayCalendarResolverTest
{
    [Fact]
    public async Task GetForYear_Canonical_ShouldReturnTenNationalEntries_AllTaggedCanonical()
    {
        var resolver = CreateResolver();

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Canonical, CancellationToken.None);

        entries.Count.ShouldBe(10);
        entries.ShouldAllBe(e => e.Source == HolidaySource.Canonical);
        entries.ShouldAllBe(e => e.Type == BrazilianHolidayType.National);
        entries.Single(e => e.Description == "Confraternização Universal").Date.ShouldBe(new DateOnly(2026, 1, 1));
        entries.Single(e => e.Description == "Sexta-feira Santa").Date.ShouldBe(new DateOnly(2026, 4, 3));
    }

    [Fact]
    public async Task GetForYear_Canonical_WithOptionalFederal_ShouldReturnThirteenEntries()
    {
        var resolver = CreateResolver();

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Canonical, CancellationToken.None);

        entries.Count.ShouldBe(13);
        entries.ShouldAllBe(e => e.Source == HolidaySource.Canonical);
        entries.Count(e => e.Type == BrazilianHolidayType.OptionalFederal).ShouldBe(3);
    }

    [Theory]
    [InlineData(1899)]
    [InlineData(2201)]
    public async Task GetForYear_ShouldThrowArgumentOutOfRangeException_WhenYearIsOutsideSupportedRange(int year)
    {
        var resolver = CreateResolver();

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            resolver.GetForYear(year, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Composite, CancellationToken.None));
    }

    [Fact]
    public async Task GetForYear_BrasilApi_Success_ShouldTagClaimedConceptsAsBrasilApi_AndBackfillRestFromCanonical()
    {
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new BrasilApiHolidayDto("2026-01-01", "Confraternização mundial", "national", "quinta-feira"),
                new BrasilApiHolidayDto("2026-04-21", "Tiradentes", "national", "terça-feira"),
                new BrasilApiHolidayDto("2026-12-25", "Natal", "national", "sexta-feira")
            ])
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.BrasilApi, CancellationToken.None);

        entries.Count.ShouldBe(10);
        entries.Single(e => e.Date == new DateOnly(2026, 1, 1)).Source.ShouldBe(HolidaySource.BrasilApi);
        entries.Single(e => e.Date == new DateOnly(2026, 1, 1)).Description.ShouldBe("Confraternização mundial");
        entries.Single(e => e.Date == new DateOnly(2026, 4, 21)).Source.ShouldBe(HolidaySource.BrasilApi);
        entries.Single(e => e.Date == new DateOnly(2026, 12, 25)).Source.ShouldBe(HolidaySource.BrasilApi);
        // Concepts not returned by BrasilAPI are backfilled from canonical.
        entries.Single(e => e.Date == new DateOnly(2026, 9, 7)).Source.ShouldBe(HolidaySource.Canonical);
        entries.Single(e => e.Date == new DateOnly(2026, 9, 7)).Description.ShouldBe("Independência");
    }

    [Fact]
    public async Task GetForYear_BrasilApi_Failure_ShouldThrowExternalProviderUnavailableException()
    {
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsFailureForYear(2026, "BrasilAPI request timed out")
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi);

        var exception = await Should.ThrowAsync<ExternalProviderUnavailableException>(() =>
            resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.BrasilApi, CancellationToken.None));
        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);
        exception.GetStatusCode.ShouldBe(502);
    }

    [Fact]
    public async Task GetForYear_Nager_Success_ShouldTagClaimedConceptsAsNager_AndBackfillRestFromCanonical()
    {
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, NagerSample2026())
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Nager, CancellationToken.None);

        entries.Count.ShouldBe(13);
        entries.Single(e => e.Date == new DateOnly(2026, 1, 1)).Source.ShouldBe(HolidaySource.Nager);
        entries.Single(e => e.Date == new DateOnly(2026, 1, 1)).Description.ShouldBe("Confraternização Universal");
        entries.Single(e => e.Date == new DateOnly(2026, 4, 21)).Source.ShouldBe(HolidaySource.Nager);
        // Quarta-feira de Cinzas is not returned by Nager — must backfill from canonical.
        entries.Single(e => e.Description == "Quarta-feira de Cinzas (até 14h)").Source.ShouldBe(HolidaySource.Canonical);
        entries.Single(e => e.Description == "Quarta-feira de Cinzas (até 14h)").Date.ShouldBe(new DateOnly(2026, 2, 18));
    }

    [Fact]
    public async Task GetForYear_Nager_Failure_ShouldThrowExternalProviderUnavailableException()
    {
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2026, "Nager.Date HTTP error: 503")
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var exception = await Should.ThrowAsync<ExternalProviderUnavailableException>(() =>
            resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Nager, CancellationToken.None));
        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task GetForYear_Nager_ShouldDropRegionalRowsWithGlobalFalse()
    {
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new NagerDateHolidayDto(
                    "2026-07-09",
                    "Revolução Constitucionalista de 1932",
                    "Constitutionalist Revolution of 1932",
                    "BR",
                    Fixed: false,
                    Global: false,
                    Counties: ["BR-SP"],
                    LaunchYear: null,
                    Types: ["Public"]),
                new NagerDateHolidayDto(
                    "2026-12-25", "Natal", "Christmas Day", "BR",
                    Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Nager, CancellationToken.None);

        // The Constitutionalist regional row must be dropped — it never matches a national concept.
        entries.Any(e => e.Date == new DateOnly(2026, 7, 9)).ShouldBeFalse();
        // The Christmas row (global=true) should claim the NATAL concept.
        entries.Single(e => e.Date == new DateOnly(2026, 12, 25)).Source.ShouldBe(HolidaySource.Nager);
    }

    [Fact]
    public async Task GetForYear_Composite_ShouldPreferNagerOverBrasilApi_WhenBothClaimSameConcept()
    {
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new BrasilApiHolidayDto("2026-12-25", "Natal", "national", "sexta-feira")
            ])
            .Build();
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new NagerDateHolidayDto(
                    "2026-12-25", "Natal", "Christmas Day", "BR",
                    Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi, nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        entries.Single(e => e.Date == new DateOnly(2026, 12, 25)).Source.ShouldBe(HolidaySource.Nager);
    }

    [Fact]
    public async Task GetForYear_Composite_ShouldFallBackToBrasilApi_WhenNagerDoesNotClaimConcept()
    {
        // Only BrasilAPI returns the Tiradentes row.
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new BrasilApiHolidayDto("2026-04-21", "Tiradentes", "national", "terça-feira")
            ])
            .Build();
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [])
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi, nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        entries.Single(e => e.Date == new DateOnly(2026, 4, 21)).Source.ShouldBe(HolidaySource.BrasilApi);
        // Everything else is canonical backfill.
        entries.Count(e => e.Source == HolidaySource.BrasilApi).ShouldBe(1);
        entries.Count(e => e.Source == HolidaySource.Canonical).ShouldBe(9);
    }

    [Fact]
    public async Task GetForYear_Composite_ShouldNeverThrow_WhenBothProvidersFail()
    {
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsFailureForYear(2026, "BrasilAPI request timed out")
            .Build();
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2026, "Nager.Date HTTP error: 503")
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi, nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        // Canonical backfills everything; no exception surfaces.
        entries.Count.ShouldBe(13);
        entries.ShouldAllBe(e => e.Source == HolidaySource.Canonical);
    }

    [Fact]
    public async Task GetForYear_Composite_ShouldClaimQuartaFeiraCinzasFromCanonical_AlwaysAndIrrespectiveOfProviderState()
    {
        // Neither provider returns Quarta-feira de Cinzas — it's always backfilled from canonical.
        var brasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [])
            .Build();
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, NagerSample2026())
            .Build();

        var resolver = CreateResolver(brasilApiProvider: brasilApi, nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        var quarta = entries.Single(e => e.Description == "Quarta-feira de Cinzas (até 14h)");
        quarta.Source.ShouldBe(HolidaySource.Canonical);
        quarta.Date.ShouldBe(new DateOnly(2026, 2, 18));
    }

    [Fact]
    public async Task GetForYear_Composite_ShouldApplyDateProximityTiebreaker_WhenNagerReturnsTwoCarnavalRowsForMonAndTue()
    {
        // Nager returns both Monday (2026-02-16) and Tuesday (2026-02-17) labeled "Carnaval".
        // Canonical CARNAVAL_TERCA for 2026 is Easter (Apr 5) − 47 = Feb 17. The tiebreaker
        // therefore picks Tuesday (distance 0) over Monday (distance 1).
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new NagerDateHolidayDto(
                    "2026-02-16", "Carnaval", "Carnival", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Bank", "Optional"]),
                new NagerDateHolidayDto(
                    "2026-02-17", "Carnaval", "Carnival", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Bank", "Optional"])
            ])
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        var carnaval = entries.Single(e => e.Type == BrazilianHolidayType.OptionalFederal && e.Description == "Carnaval");
        carnaval.Date.ShouldBe(new DateOnly(2026, 2, 17));
        carnaval.Source.ShouldBe(HolidaySource.Nager);
    }

    [Fact]
    public async Task GetForYear_Composite_Carnaval_ShouldOverrideCanonicalDate_WhenNagerAndCanonicalDisagreeByOneDay()
    {
        // Literal "Nager day-X vs Canonical day-Y Carnaval" hypothetical: Nager returns
        // ONLY one Carnaval row, on a date that disagrees with the canonical Meeus/Jones/Butcher
        // expected date. Per the Phase 6.5 contract, the provider's date wins — name match
        // alone is enough to claim the concept and the row's date overrides expected.
        //
        // For 2029, canonical Easter = Apr 1 (Sun) → canonical Carnaval terça = Feb 13 (Tue).
        // The Nager fixture returns Carnaval on Feb 12 (Mon) — one day before canonical.
        // The resolver must claim CARNAVAL_TERCA with the provider's Feb 12 date, not Feb 13.
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2029, [
                new NagerDateHolidayDto(
                    "2029-02-12", "Carnaval", "Carnival", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Bank", "Optional"])
            ])
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var entries = await resolver.GetForYear(2029, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        var carnaval = entries.Single(e => e.Type == BrazilianHolidayType.OptionalFederal && e.Description == "Carnaval");
        carnaval.Date.ShouldBe(new DateOnly(2029, 2, 12)); // Nager's date, NOT canonical's Feb 13.
        carnaval.Source.ShouldBe(HolidaySource.Nager);

        // Sanity check: Quarta-feira de Cinzas (Easter−46) still backfills from canonical
        // at its proper Feb 14 date — Nager doesn't return it, and the off-Carnaval date
        // from Nager must not shift the cinzas backfill.
        var cinzas = entries.Single(e => e.Description == "Quarta-feira de Cinzas (até 14h)");
        cinzas.Date.ShouldBe(new DateOnly(2029, 2, 14));
        cinzas.Source.ShouldBe(HolidaySource.Canonical);
    }

    [Fact]
    public async Task GetForYear_Nager_ShouldStillMatch_WhenProviderDateIsFarFromExpected()
    {
        // Name-match wins even when the provider date is multiple days off the catalog's expected.
        // The provider's date overrides expected.
        var nager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2026, [
                new NagerDateHolidayDto(
                    "2026-04-26", "Dia de Tiradentes", "Tiradentes", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        var resolver = CreateResolver(nagerProvider: nager);

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Nager, CancellationToken.None);

        var tiradentes = entries.Single(e => e.Description == "Dia de Tiradentes");
        tiradentes.Date.ShouldBe(new DateOnly(2026, 4, 26));
        tiradentes.Source.ShouldBe(HolidaySource.Nager);
    }

    [Fact]
    public async Task GetForYear_AllSources_ShouldOrderEntriesByDateThenType()
    {
        var resolver = CreateResolver();

        var entries = await resolver.GetForYear(2026, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Canonical, CancellationToken.None);

        var sorted = entries.OrderBy(e => e.Date).ThenBy(e => e.Type).ToList();
        entries.ShouldBe(sorted);
    }

    private static BrazilianHolidayCalendarResolver CreateResolver(
        IBrasilApiHolidayProvider? brasilApiProvider = null,
        INagerDateHolidayProvider? nagerProvider = null)
    {
        return new BrazilianHolidayCalendarResolver(
            new BrazilianHolidayCalendar(),
            brasilApiProvider ?? new BrasilApiHolidayProviderBuilder().Build(),
            nagerProvider ?? new NagerDateHolidayProviderBuilder().Build(),
            NullLogger<BrazilianHolidayCalendarResolver>.Instance);
    }

    private static IReadOnlyList<NagerDateHolidayDto> NagerSample2026() =>
    [
        new("2026-01-01", "Confraternização Universal", "New Year's Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-02-16", "Carnaval", "Carnival", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Bank", "Optional"]),
        new("2026-02-17", "Carnaval", "Carnival", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Bank", "Optional"]),
        new("2026-04-03", "Sexta-feira Santa", "Good Friday", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-04-21", "Dia de Tiradentes", "Tiradentes", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-05-01", "Dia do Trabalhador", "Labour Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-06-04", "Corpus Christi", "Corpus Christi", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-09-07", "Dia da Independência", "Independence Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-10-12", "Nossa Senhora Aparecida", "Our Lady of Aparecida", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-11-02", "Dia de Finados", "All Souls' Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-11-15", "Proclamação da República", "Republic Proclamation Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-11-20", "Dia da Consciência Negra", "Black Awareness Day", "BR",
            Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
        new("2026-12-25", "Natal", "Christmas Day", "BR",
            Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
    ];
}
