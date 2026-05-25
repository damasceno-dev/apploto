using server.Application.Services.Holidays;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Holidays;

/// <summary>
/// Verifies the catalog-level contract of <see cref="BrazilianHolidayConceptCatalog"/>
/// + <see cref="BrazilianHolidayConceptMatcher"/>:
///
/// 1. Data integrity — 13 concepts split 10/3, stable unique IDs, deterministic
///    <c>ExpectedDateForYear</c> for both fixed-date and Easter-derived concepts.
/// 2. Name-pattern matching across canonical and typical provider descriptions
///    (BrasilAPI + Nager.Date), including "Dia da consciência negra" → CONSCIENCIA_NEGRA.
/// 3. Date-proximity tiebreaker — when multiple rows share a matching name,
///    the row whose date is closest to <c>ExpectedDateForYear</c> wins.
/// 4. "Name matches but date is days off" still matches — the closest-date
///    tiebreaker does not apply a hard cap, so a provider's authoritative date
///    overrides the catalog's expected date.
/// 5. Unmatched rows — provider rows whose normalized search text matches no
///    concept matcher return <c>null</c> from <c>TryFindConceptForRow</c>.
///
/// End-to-end source-dispatch behaviors that combine the matcher with provider
/// failure surfaces (composite priority, 502 translation, canonical backfill)
/// live in <c>BrazilianHolidayCalendarResolverTest</c>.
/// </summary>
public class BrazilianHolidayConceptCatalogTest
{
    private static BrazilianHolidayProviderRow Row(DateOnly date, string description) =>
        new(date, description, BrazilianHolidayTextNormalizer.Normalize(description));

    [Fact]
    public void Catalog_ShouldExposeExactlyThirteenConcepts()
    {
        BrazilianHolidayConceptCatalog.All.Count.ShouldBe(13);
    }

    [Fact]
    public void Catalog_ShouldExposeTenNationalAndThreeOptionalFederalConcepts()
    {
        BrazilianHolidayConceptCatalog.All.Count(c => c.Type == BrazilianHolidayType.National).ShouldBe(10);
        BrazilianHolidayConceptCatalog.All.Count(c => c.Type == BrazilianHolidayType.OptionalFederal).ShouldBe(3);
    }

    [Fact]
    public void Catalog_ShouldExposeStableUniqueConceptIds()
    {
        var ids = BrazilianHolidayConceptCatalog.All.Select(c => c.ConceptId).ToList();
        ids.Distinct().Count().ShouldBe(ids.Count);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.ConfraternizacaoUniversal);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.CarnavalTerca);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.QuartaFeiraCinzas);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.SextaFeiraSanta);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.Tiradentes);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.DiaDoTrabalho);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.CorpusChristi);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.Independencia);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.NossaSenhoraAparecida);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.Finados);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.ProclamacaoRepublica);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.ConscienciaNegra);
        ids.ShouldContain(BrazilianHolidayConceptCatalog.Natal);
    }

    [Theory]
    [InlineData(BrazilianHolidayConceptCatalog.ConfraternizacaoUniversal, 2026, 1, 1)]
    [InlineData(BrazilianHolidayConceptCatalog.Tiradentes, 2026, 4, 21)]
    [InlineData(BrazilianHolidayConceptCatalog.DiaDoTrabalho, 2026, 5, 1)]
    [InlineData(BrazilianHolidayConceptCatalog.Independencia, 2026, 9, 7)]
    [InlineData(BrazilianHolidayConceptCatalog.NossaSenhoraAparecida, 2026, 10, 12)]
    [InlineData(BrazilianHolidayConceptCatalog.Finados, 2026, 11, 2)]
    [InlineData(BrazilianHolidayConceptCatalog.ProclamacaoRepublica, 2026, 11, 15)]
    [InlineData(BrazilianHolidayConceptCatalog.ConscienciaNegra, 2026, 11, 20)]
    [InlineData(BrazilianHolidayConceptCatalog.Natal, 2026, 12, 25)]
    [InlineData(BrazilianHolidayConceptCatalog.SextaFeiraSanta, 2026, 4, 3)]
    [InlineData(BrazilianHolidayConceptCatalog.CarnavalTerca, 2026, 2, 17)]
    [InlineData(BrazilianHolidayConceptCatalog.QuartaFeiraCinzas, 2026, 2, 18)]
    [InlineData(BrazilianHolidayConceptCatalog.CorpusChristi, 2026, 6, 4)]
    public void ExpectedDateForYear_ShouldReturnCanonicalDate_ForKnownYear(
        string conceptId,
        int year,
        int expectedMonth,
        int expectedDay)
    {
        var concept = BrazilianHolidayConceptCatalog.All.Single(c => c.ConceptId == conceptId);

        concept.ExpectedDateForYear(year).ShouldBe(new DateOnly(year, expectedMonth, expectedDay));
    }

    [Theory]
    [InlineData(BrazilianHolidayConceptCatalog.ConfraternizacaoUniversal, "Confraternização Universal")]
    [InlineData(BrazilianHolidayConceptCatalog.ConfraternizacaoUniversal, "Confraternização mundial")]
    [InlineData(BrazilianHolidayConceptCatalog.CarnavalTerca, "Carnaval")]
    [InlineData(BrazilianHolidayConceptCatalog.CarnavalTerca, "Carnaval (terça)")]
    [InlineData(BrazilianHolidayConceptCatalog.QuartaFeiraCinzas, "Quarta-feira de Cinzas (até 14h)")]
    [InlineData(BrazilianHolidayConceptCatalog.QuartaFeiraCinzas, "Ash Wednesday")]
    [InlineData(BrazilianHolidayConceptCatalog.SextaFeiraSanta, "Sexta-feira Santa")]
    [InlineData(BrazilianHolidayConceptCatalog.SextaFeiraSanta, "Good Friday")]
    [InlineData(BrazilianHolidayConceptCatalog.Tiradentes, "Tiradentes")]
    [InlineData(BrazilianHolidayConceptCatalog.Tiradentes, "Dia de Tiradentes")]
    [InlineData(BrazilianHolidayConceptCatalog.DiaDoTrabalho, "Dia do Trabalho")]
    [InlineData(BrazilianHolidayConceptCatalog.DiaDoTrabalho, "Dia do trabalho")]
    [InlineData(BrazilianHolidayConceptCatalog.DiaDoTrabalho, "Dia do Trabalhador")]
    [InlineData(BrazilianHolidayConceptCatalog.DiaDoTrabalho, "Labour Day")]
    [InlineData(BrazilianHolidayConceptCatalog.CorpusChristi, "Corpus Christi")]
    [InlineData(BrazilianHolidayConceptCatalog.Independencia, "Independência")]
    [InlineData(BrazilianHolidayConceptCatalog.Independencia, "Independência do Brasil")]
    [InlineData(BrazilianHolidayConceptCatalog.Independencia, "Dia da Independência")]
    [InlineData(BrazilianHolidayConceptCatalog.NossaSenhoraAparecida, "Nossa Senhora Aparecida")]
    [InlineData(BrazilianHolidayConceptCatalog.Finados, "Finados")]
    [InlineData(BrazilianHolidayConceptCatalog.Finados, "Dia de Finados")]
    [InlineData(BrazilianHolidayConceptCatalog.ProclamacaoRepublica, "Proclamação da República")]
    [InlineData(BrazilianHolidayConceptCatalog.ConscienciaNegra, "Consciência Negra")]
    [InlineData(BrazilianHolidayConceptCatalog.ConscienciaNegra, "Dia da consciência negra")]
    [InlineData(BrazilianHolidayConceptCatalog.ConscienciaNegra, "Dia da Consciência Negra")]
    [InlineData(BrazilianHolidayConceptCatalog.Natal, "Natal")]
    [InlineData(BrazilianHolidayConceptCatalog.Natal, "Christmas Day")]
    public void NameMatchers_ShouldMatchCanonicalAndProviderDescriptions_AfterNormalization(
        string conceptId,
        string providerDescription)
    {
        var concept = BrazilianHolidayConceptCatalog.All.Single(c => c.ConceptId == conceptId);
        var normalized = BrazilianHolidayTextNormalizer.Normalize(providerDescription);

        concept.NameMatchers
            .Any(normalized.Contains)
            .ShouldBeTrue(
                $"Concept {conceptId} matchers [{string.Join(", ", concept.NameMatchers)}] should match \"{providerDescription}\" (normalized: \"{normalized}\")");
    }

    [Fact]
    public void NameMatchers_ShouldNotCrossMatchUnrelatedConcept_TiradentesVsIndependencia()
    {
        // Sanity check that the matchers stay distinct for two same-month concepts.
        var tiradentes = BrazilianHolidayConceptCatalog.All.Single(c => c.ConceptId == BrazilianHolidayConceptCatalog.Tiradentes);
        var independencia = BrazilianHolidayConceptCatalog.All.Single(c => c.ConceptId == BrazilianHolidayConceptCatalog.Independencia);

        var normalizedTiradentes = BrazilianHolidayTextNormalizer.Normalize("Tiradentes");
        var normalizedIndependencia = BrazilianHolidayTextNormalizer.Normalize("Independência");

        tiradentes.NameMatchers.Any(normalizedIndependencia.Contains).ShouldBeFalse();
        independencia.NameMatchers.Any(normalizedTiradentes.Contains).ShouldBeFalse();
    }

    [Fact]
    public void NameMatchers_ShouldDropUnrelatedProviderRows_AfterNormalization()
    {
        // Provider rows that aren't national/canonical concepts (e.g. Páscoa, Domingo de Páscoa,
        // Easter Sunday from BrasilAPI/Nager.Date) must not match any concept.
        var unrelatedDescriptions = new[]
        {
            "Páscoa",
            "Domingo de Páscoa",
            "Easter Sunday",
            "Revolução Constitucionalista de 1932"
        };

        foreach (var description in unrelatedDescriptions)
        {
            var normalized = BrazilianHolidayTextNormalizer.Normalize(description);
            var anyConceptMatches = BrazilianHolidayConceptCatalog.All
                .Any(concept => concept.NameMatchers.Any(normalized.Contains));

            anyConceptMatches.ShouldBeFalse(
                $"\"{description}\" (normalized: \"{normalized}\") must not match any catalog concept");
        }
    }

    // -------------------------------------------------------------------------
    // Matcher behaviors (direct against BrazilianHolidayConceptMatcher).
    // These cover the contract assigned by milestone item 6.5.17:
    //   - name-pattern matching for every concept (asserted via the parameterized
    //     test above plus the single-row matcher below);
    //   - date-proximity tiebreaker (two same-name candidates → closer to expected wins);
    //   - "name matches but date is days off" still matches (priority source date
    //     overrides expected);
    //   - unmatched provider rows return null.
    // -------------------------------------------------------------------------

    [Fact]
    public void TryFindConceptForRow_ShouldReturnNull_WhenSearchTextMatchesNoConcept()
    {
        // Two unrelated provider descriptions: a holy day (Páscoa) and a regional
        // commemoration (Revolução Constitucionalista). Neither maps to any concept.
        var paschal = Row(new DateOnly(2026, 4, 5), "Páscoa");
        var regional = Row(new DateOnly(2026, 7, 9), "Revolução Constitucionalista de 1932");

        BrazilianHolidayConceptMatcher.TryFindConceptForRow(paschal, 2026).ShouldBeNull();
        BrazilianHolidayConceptMatcher.TryFindConceptForRow(regional, 2026).ShouldBeNull();
    }

    [Fact]
    public void TryFindConceptForRow_ShouldClaimConsciuenciaNegra_FromBrasilApiVariantSpelling()
    {
        // BrasilAPI's literal spelling for Consciência Negra is "Dia da consciência negra".
        // Normalized substring matching must claim CONSCIENCIA_NEGRA.
        var row = Row(new DateOnly(2026, 11, 20), "Dia da consciência negra");

        var concept = BrazilianHolidayConceptMatcher.TryFindConceptForRow(row, 2026);

        concept.ShouldNotBeNull();
        concept.ConceptId.ShouldBe(BrazilianHolidayConceptCatalog.ConscienciaNegra);
    }

    [Fact]
    public void TryFindConceptForRow_ShouldReturnNull_WhenOnlyMatchIsOptionalFederalAndIncludeOptionalFederalIsFalse()
    {
        // "Carnaval" matches only CARNAVAL_TERCA (an optional-federal concept).
        // With includeOptionalFederal: false, this concept is filtered out → no match.
        var row = Row(new DateOnly(2026, 2, 17), "Carnaval");

        BrazilianHolidayConceptMatcher
            .TryFindConceptForRow(row, 2026, includeOptionalFederal: false)
            .ShouldBeNull();
    }

    [Fact]
    public void ClaimsByConcept_DateProximityTiebreaker_ShouldPickRowClosestToExpected_AmongSameNameCandidates()
    {
        // Two "Carnaval" rows in 2026 — Monday (Feb 16) and Tuesday (Feb 17).
        // Canonical Carnaval terça for 2026 = Easter (Apr 5) - 47 days = Feb 17.
        // Distance: Feb 16 = 1 day, Feb 17 = 0 days → Tuesday wins.
        var monday = Row(new DateOnly(2026, 2, 16), "Carnaval");
        var tuesday = Row(new DateOnly(2026, 2, 17), "Carnaval");

        var claims = BrazilianHolidayConceptMatcher.ClaimsByConcept(
            [monday, tuesday], 2026, includeOptionalFederal: true);

        claims.ShouldContainKey(BrazilianHolidayConceptCatalog.CarnavalTerca);
        claims[BrazilianHolidayConceptCatalog.CarnavalTerca].Date.ShouldBe(new DateOnly(2026, 2, 17));
    }

    [Fact]
    public void ClaimsByConcept_DateProximityTiebreaker_ShouldPickRowClosestToExpected_WhenBothCandidatesAreOffByDifferentAmounts()
    {
        // Two same-name candidates, neither matches the canonical date exactly.
        // Canonical Tiradentes = Apr 21. Rows: Apr 23 (+2) and Apr 25 (+4). Apr 23 wins.
        var nearer = Row(new DateOnly(2026, 4, 23), "Tiradentes");
        var farther = Row(new DateOnly(2026, 4, 25), "Tiradentes");

        var claims = BrazilianHolidayConceptMatcher.ClaimsByConcept(
            [nearer, farther], 2026, includeOptionalFederal: false);

        claims.ShouldContainKey(BrazilianHolidayConceptCatalog.Tiradentes);
        claims[BrazilianHolidayConceptCatalog.Tiradentes].Date.ShouldBe(new DateOnly(2026, 4, 23));
    }

    [Fact]
    public void TryFindConceptForRow_NameMatch_ShouldStillClaim_WhenDateIsFiveDaysOffExpected()
    {
        // Catalog expected Tiradentes 2026 = Apr 21. Provider row reports Apr 26 (+5 days).
        // Name match still claims the concept; the closest-date tiebreaker doesn't apply
        // a hard cap, so the provider's date will end up overriding expected in the resolver.
        var fiveDaysOff = Row(new DateOnly(2026, 4, 26), "Tiradentes");

        var concept = BrazilianHolidayConceptMatcher.TryFindConceptForRow(fiveDaysOff, 2026);

        concept.ShouldNotBeNull();
        concept.ConceptId.ShouldBe(BrazilianHolidayConceptCatalog.Tiradentes);
    }

    [Fact]
    public void ClaimsByConcept_NameMatch_ShouldStillClaim_WhenOnlyCandidateIsFiveDaysOffExpected()
    {
        // Only one candidate, name-matching but five days off the canonical date.
        // The claim still goes through and surfaces the row's date (not the canonical date).
        var fiveDaysOff = Row(new DateOnly(2026, 4, 26), "Tiradentes");

        var claims = BrazilianHolidayConceptMatcher.ClaimsByConcept(
            [fiveDaysOff], 2026, includeOptionalFederal: false);

        claims.ShouldContainKey(BrazilianHolidayConceptCatalog.Tiradentes);
        claims[BrazilianHolidayConceptCatalog.Tiradentes].Date.ShouldBe(new DateOnly(2026, 4, 26));
    }

    [Fact]
    public void ClaimsByConcept_ShouldDropUnmatchedRows_AndClaimOnlyMatchedConcepts()
    {
        // Mix three rows: two valid concepts + one unrelated. Unrelated row must be dropped
        // (not appear under any concept), the two valid rows must each claim their concept.
        var natal = Row(new DateOnly(2026, 12, 25), "Natal");
        var pascoa = Row(new DateOnly(2026, 4, 5), "Páscoa");
        var tiradentes = Row(new DateOnly(2026, 4, 21), "Tiradentes");

        var claims = BrazilianHolidayConceptMatcher.ClaimsByConcept(
            [natal, pascoa, tiradentes], 2026, includeOptionalFederal: true);

        claims.ShouldContainKey(BrazilianHolidayConceptCatalog.Natal);
        claims.ShouldContainKey(BrazilianHolidayConceptCatalog.Tiradentes);
        claims.Values.ShouldNotContain(pascoa);
        // Páscoa's date never surfaces under any concept.
        claims.Values.Any(r => r.Date == new DateOnly(2026, 4, 5)).ShouldBeFalse();
    }
}
