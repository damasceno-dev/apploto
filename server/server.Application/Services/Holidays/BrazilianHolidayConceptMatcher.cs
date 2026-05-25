using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

/// <summary>
/// Owns the catalog-level matching contract: given a provider row (or a list of rows),
/// resolves which catalog concept it claims. Matching is name-pattern first
/// (accent-stripped lowercased substring match against the concept's
/// <c>NameMatchers</c>); when more than one concept matches a single row, or more than
/// one row matches a single concept, the candidate whose date is closest to the
/// concept's <c>ExpectedDateForYear</c> wins. There is no hard ±3-day cap — a name
/// match with a date further off still claims the concept, so the winning provider's
/// date overrides the canonical expected date.
///
/// Living next to <see cref="BrazilianHolidayConceptCatalog"/> means the matching
/// contract can be exercised directly from <c>BrazilianHolidayConceptCatalogTest</c>
/// without going through the resolver — the resolver delegates here for the actual
/// algorithm, and only layers the source-dispatch and exception-translation logic
/// on top.
/// </summary>
internal static class BrazilianHolidayConceptMatcher
{
    /// <summary>
    /// Finds the catalog concept best claimed by a single provider row.
    /// Returns <c>null</c> when no concept's <c>NameMatchers</c> appear as a substring
    /// of the row's normalized <c>SearchText</c>.
    /// </summary>
    /// <param name="row">A normalized provider row.</param>
    /// <param name="year">Gregorian year used to compute each concept's expected date.</param>
    /// <param name="includeOptionalFederal">
    /// When <c>false</c>, optional-federal concepts are excluded from matching — provider
    /// rows that would only match an optional-federal concept return <c>null</c>.
    /// </param>
    /// <returns>
    /// The closest-by-expected-date catalog concept whose matchers hit the row's
    /// <c>SearchText</c>, or <c>null</c> if no concept matches.
    /// </returns>
    public static BrazilianHolidayConcept? TryFindConceptForRow(
        BrazilianHolidayProviderRow row,
        int year,
        bool includeOptionalFederal = true)
    {
        BrazilianHolidayConcept? best = null;
        var bestDistance = int.MaxValue;

        foreach (var concept in BrazilianHolidayConceptCatalog.All)
        {
            if (!includeOptionalFederal && concept.Type != BrazilianHolidayType.National)
                continue;

            if (!concept.NameMatchers.Any(matcher => row.SearchText.Contains(matcher)))
                continue;

            var distance = Math.Abs(row.Date.DayNumber - concept.ExpectedDateForYear(year).DayNumber);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = concept;
            }
        }

        return best;
    }

    /// <summary>
    /// For every catalog concept (filtered by <paramref name="includeOptionalFederal"/>),
    /// finds the best-claiming provider row: among rows whose <c>SearchText</c> contains
    /// any of the concept's matchers, the row whose date is closest to the concept's
    /// <c>ExpectedDateForYear(year)</c> wins. Concepts that no row matches are absent
    /// from the result.
    /// </summary>
    /// <param name="rows">Normalized provider rows.</param>
    /// <param name="year">Gregorian year used for the per-concept expected dates.</param>
    /// <param name="includeOptionalFederal">Whether optional-federal concepts participate.</param>
    /// <returns>A dictionary keyed by <c>ConceptId</c> with the winning row per claimed concept.</returns>
    public static IReadOnlyDictionary<string, BrazilianHolidayProviderRow> ClaimsByConcept(
        IReadOnlyList<BrazilianHolidayProviderRow> rows,
        int year,
        bool includeOptionalFederal)
    {
        var claims = new Dictionary<string, BrazilianHolidayProviderRow>();

        foreach (var concept in BrazilianHolidayConceptCatalog.All)
        {
            if (!includeOptionalFederal && concept.Type != BrazilianHolidayType.National)
                continue;

            var expected = concept.ExpectedDateForYear(year);

            BrazilianHolidayProviderRow? best = null;
            var bestDistance = int.MaxValue;

            foreach (var row in rows)
            {
                if (!concept.NameMatchers.Any(matcher => row.SearchText.Contains(matcher)))
                    continue;

                var distance = Math.Abs(row.Date.DayNumber - expected.DayNumber);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = row;
                }
            }

            if (best is not null)
                claims[concept.ConceptId] = best;
        }

        return claims;
    }
}
