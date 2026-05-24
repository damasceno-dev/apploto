using Microsoft.Extensions.Logging;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces.Holidays;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Holidays;

/// <summary>
/// Resolves the Brazilian holiday calendar for a given year by dispatching to one of four
/// sources: the canonical Meeus/Jones/Butcher-derived calendar, a single external provider
/// (BrasilAPI or Nager.Date), or a composite that walks both providers in priority order
/// and backfills any unclaimed concepts from the canonical calendar.
///
/// This is the single owner of the Infrastructure-failure → backend-exception translation.
/// Provider clients communicate failure by returning a <see cref="BrazilianHolidayProviderResult{T}"/>
/// with <c>Success = false</c>; this resolver decides whether to surface it as
/// <see cref="ExternalProviderUnavailableException"/> (explicit single-source requests) or
/// swallow it and continue (composite requests, where canonical always backfills).
/// </summary>
internal sealed class BrazilianHolidayCalendarResolver(
    IBrazilianHolidayCalendar canonicalCalendar,
    IBrasilApiHolidayProvider brasilApiProvider,
    INagerDateHolidayProvider nagerProvider,
    ILogger<BrazilianHolidayCalendarResolver> logger)
    : IBrazilianHolidayCalendarResolver
{
    private const int MinimumYear = 1900;
    private const int MaximumYear = 2200;

    /// <summary>
    /// Resolves the Brazilian holiday calendar entries for the requested year, honoring the
    /// caller's source selection and optional-federal flag.
    /// </summary>
    /// <param name="year">
    /// The target Gregorian year. Must fall within <c>[1900, 2200]</c>; values outside that
    /// range throw <see cref="ArgumentOutOfRangeException"/> so the calling use case can
    /// translate the error into a validation-shaped 400 response.
    /// </param>
    /// <param name="includeOptionalFederal">
    /// When <c>true</c>, includes the three curated MGI optional-federal entries
    /// (Carnaval terça, Quarta-feira de Cinzas, Corpus Christi) in addition to the ten
    /// mandatory national holidays. When <c>false</c>, returns the ten national entries only.
    /// </param>
    /// <param name="source">
    /// Which source path to use:
    /// <list type="bullet">
    ///   <item><see cref="BrazilianHolidayCalendarSource.Canonical"/>: pure canonical calendar (offline, deterministic).</item>
    ///   <item><see cref="BrazilianHolidayCalendarSource.BrasilApi"/>: BrasilAPI primary, canonical backfill for unclaimed concepts.</item>
    ///   <item><see cref="BrazilianHolidayCalendarSource.Nager"/>: Nager.Date primary, canonical backfill for unclaimed concepts.</item>
    ///   <item><see cref="BrazilianHolidayCalendarSource.Composite"/>: Nager first, BrasilAPI second, canonical backfill last.</item>
    /// </list>
    /// </param>
    /// <param name="cancellationToken">Cancellation token forwarded to the provider HTTP calls.</param>
    /// <returns>
    /// A list of <see cref="SourcedBrazilianHolidayEntry"/> values, ordered ascending by
    /// <c>Date</c> and then by <c>Type</c>, with each entry's <c>Source</c> tag indicating
    /// the provenance that actually claimed that concept.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="year"/> is outside <c>[1900, 2200]</c>.
    /// </exception>
    /// <exception cref="ExternalProviderUnavailableException">
    /// Thrown only when <paramref name="source"/> is an explicit external provider
    /// (<see cref="BrazilianHolidayCalendarSource.BrasilApi"/> or
    /// <see cref="BrazilianHolidayCalendarSource.Nager"/>) and that provider returns a
    /// failed <see cref="BrazilianHolidayProviderResult{T}"/>. Composite never throws this.
    /// </exception>
    public async Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> GetForYear(int year,bool includeOptionalFederal,BrazilianHolidayCalendarSource source,CancellationToken cancellationToken)
    {
        if (year is < MinimumYear or > MaximumYear)
            throw new ArgumentOutOfRangeException(nameof(year),year,ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE);

        return source switch
        {
            BrazilianHolidayCalendarSource.Canonical =>
                BuildCanonical(year, includeOptionalFederal),
            BrazilianHolidayCalendarSource.BrasilApi =>
                await BuildSingleSourceFromBrasilApi(year, includeOptionalFederal, cancellationToken),
            BrazilianHolidayCalendarSource.Nager =>
                await BuildSingleSourceFromNager(year, includeOptionalFederal, cancellationToken),
            BrazilianHolidayCalendarSource.Composite =>
                await BuildComposite(year, includeOptionalFederal, cancellationToken),
            _ => BuildCanonical(year, includeOptionalFederal)
        };
    }

    /// <summary>
    /// Builds the result list straight from the canonical
    /// <see cref="IBrazilianHolidayCalendar"/>, tagging every entry with
    /// <see cref="HolidaySource.Canonical"/>. No external I/O occurs on this path.
    /// </summary>
    /// <param name="year">Gregorian year to resolve (already range-checked by the caller).</param>
    /// <param name="includeOptionalFederal">Whether to include the three optional-federal entries.</param>
    /// <returns>
    /// The canonical 10/13 entries, ordered by <c>Date</c> then <c>Type</c>, each tagged
    /// <see cref="HolidaySource.Canonical"/>.
    /// </returns>
    private List<SourcedBrazilianHolidayEntry> BuildCanonical(
        int year,
        bool includeOptionalFederal)
    {
        return canonicalCalendar.GetForYear(year, includeOptionalFederal)
            .Select(entry => new SourcedBrazilianHolidayEntry(
                entry.Date,
                entry.Description,
                entry.Type,
                HolidaySource.Canonical))
            .ToList();
    }

    /// <summary>
    /// Resolves the calendar exclusively from BrasilAPI: calls the provider, maps each
    /// returned DTO to a catalog concept via name + date-proximity matching, drops
    /// unmatched rows, and backfills any concept the provider did not claim from the
    /// canonical calendar (tagged <see cref="HolidaySource.Canonical"/>).
    /// </summary>
    /// <param name="year">Gregorian year to resolve.</param>
    /// <param name="includeOptionalFederal">Whether to include the three optional-federal entries.</param>
    /// <param name="cancellationToken">Token forwarded to the BrasilAPI HTTP call.</param>
    /// <returns>
    /// Concept-tagged entries with each provider-claimed concept tagged
    /// <see cref="HolidaySource.BrasilApi"/> and any backfilled concept tagged
    /// <see cref="HolidaySource.Canonical"/>. Ordered by <c>Date</c> then <c>Type</c>.
    /// </returns>
    /// <exception cref="ExternalProviderUnavailableException">
    /// Thrown when BrasilAPI returns a failed <see cref="BrazilianHolidayProviderResult{T}"/>
    /// (timeout, non-2xx, malformed JSON, etc.). This is the single translation point
    /// from a failed Infrastructure result into a backend 502 exception.
    /// </exception>
    private async Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> BuildSingleSourceFromBrasilApi(int year,bool includeOptionalFederal,CancellationToken cancellationToken)
    {
        var result = await brasilApiProvider.GetHolidaysForYear(year, cancellationToken);

        if (result.Success is false || result.Data is null)
            throw new ExternalProviderUnavailableException(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);

        var rows = NormalizeBrasilApiRows(result.Data);
        return BuildFromClaims(year, includeOptionalFederal, MatchToCatalog(rows, year, includeOptionalFederal));
    }

    /// <summary>
    /// Resolves the calendar exclusively from Nager.Date: calls the provider, drops
    /// regional rows (<c>Global = false</c>), maps each remaining DTO to a catalog concept
    /// via name + date-proximity matching, drops unmatched rows, and backfills any concept
    /// the provider did not claim from the canonical calendar
    /// (tagged <see cref="HolidaySource.Canonical"/>).
    /// </summary>
    /// <param name="year">Gregorian year to resolve.</param>
    /// <param name="includeOptionalFederal">Whether to include the three optional-federal entries.</param>
    /// <param name="cancellationToken">Token forwarded to the Nager.Date HTTP call.</param>
    /// <returns>
    /// Concept-tagged entries with each provider-claimed concept tagged
    /// <see cref="HolidaySource.Nager"/> and any backfilled concept tagged
    /// <see cref="HolidaySource.Canonical"/>. Ordered by <c>Date</c> then <c>Type</c>.
    /// </returns>
    /// <exception cref="ExternalProviderUnavailableException">
    /// Thrown when Nager.Date returns a failed <see cref="BrazilianHolidayProviderResult{T}"/>.
    /// </exception>
    private async Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> BuildSingleSourceFromNager(int year,bool includeOptionalFederal,CancellationToken cancellationToken)
    {
        var result = await nagerProvider.GetHolidaysForYear(year, cancellationToken);

        if (!result.Success || result.Data is null)
            throw new ExternalProviderUnavailableException(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);

        var rows = NormalizeNagerRows(result.Data);
        return BuildFromClaims(year, includeOptionalFederal, MatchToCatalog(rows, year, includeOptionalFederal));
    }

    /// <summary>
    /// Resolves the calendar from both external providers, with Nager.Date taking priority
    /// over BrasilAPI. Each provider's failure is logged at warning level and silently
    /// skipped, so this method never throws <see cref="ExternalProviderUnavailableException"/>;
    /// any concept left unclaimed after both providers run is backfilled from the canonical
    /// calendar (tagged <see cref="HolidaySource.Canonical"/>).
    /// </summary>
    /// <param name="year">Gregorian year to resolve.</param>
    /// <param name="includeOptionalFederal">Whether to include the three optional-federal entries.</param>
    /// <param name="cancellationToken">Token forwarded to both provider HTTP calls.</param>
    /// <returns>
    /// Concept-tagged entries with each concept tagged by the first provider that claimed
    /// it (Nager → BrasilAPI), or <see cref="HolidaySource.Canonical"/> when no provider
    /// claimed it. Ordered by <c>Date</c> then <c>Type</c>.
    /// </returns>
    private async Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> BuildComposite(int year,bool includeOptionalFederal,CancellationToken cancellationToken)
    {
        var claims = new Dictionary<string, ProviderClaim>();

        var nagerResult = await nagerProvider.GetHolidaysForYear(year, cancellationToken);
        if (nagerResult is { Success: true, Data: not null })
        {
            var nagerClaims = MatchToCatalog(NormalizeNagerRows(nagerResult.Data), year, includeOptionalFederal);
            MergeClaims(claims, nagerClaims);
        }
        else
        {
            logger.LogWarning("Nager.Date provider unavailable in composite resolver: {Reason}",nagerResult.FailureReason);
        }

        var brasilApiResult = await brasilApiProvider.GetHolidaysForYear(year, cancellationToken);
        if (brasilApiResult is { Success: true, Data: not null })
        {
            var brasilApiClaims = MatchToCatalog(NormalizeBrasilApiRows(brasilApiResult.Data), year, includeOptionalFederal);
            MergeClaims(claims, brasilApiClaims);
        }
        else
        {
            logger.LogWarning("BrasilAPI provider unavailable in composite resolver: {Reason}",brasilApiResult.FailureReason);
        }

        return BuildFromClaims(year, includeOptionalFederal, claims);
    }

    /// <summary>
    /// Copies claims from <paramref name="incoming"/> into <paramref name="destination"/>
    /// using priority-preserving semantics: a concept already present in
    /// <paramref name="destination"/> is left untouched (first-writer wins), so callers
    /// can layer lower-priority providers on top of higher-priority ones safely.
    /// </summary>
    /// <param name="destination">The accumulator dictionary that retains existing claims.</param>
    /// <param name="incoming">The new dictionary whose unseen entries should be added.</param>
    private static void MergeClaims(Dictionary<string, ProviderClaim> destination,Dictionary<string, ProviderClaim> incoming)
    {
        foreach (var (conceptId, claim) in incoming)
        {
            destination.TryAdd(conceptId, claim);
        }
    }

    /// <summary>
    /// Normalizes BrasilAPI DTOs into the shared <see cref="ProviderRow"/> shape used by
    /// the catalog matcher. Rows are dropped silently when they lack a parseable date or
    /// a non-empty name. <c>SearchText</c> is the accent-stripped lowercased
    /// <see cref="BrasilApiHolidayDto.Name"/>.
    /// </summary>
    /// <param name="dtos">BrasilAPI response DTOs.</param>
    /// <returns>A list of normalized provider rows tagged <see cref="HolidaySource.BrasilApi"/>.</returns>
    private static List<ProviderRow> NormalizeBrasilApiRows(IReadOnlyList<BrasilApiHolidayDto> dtos)
    {
        var rows = new List<ProviderRow>(dtos.Count);

        foreach (var dto in dtos)
        {
            if (string.IsNullOrWhiteSpace(dto.Date) || string.IsNullOrWhiteSpace(dto.Name))
                continue;

            if (DateOnly.TryParse(dto.Date, out var date) is false)
                continue;

            rows.Add(new ProviderRow(date,dto.Name,HolidaySource.BrasilApi,BrazilianHolidayTextNormalizer.Normalize(dto.Name)));
        }

        return rows;
    }

    /// <summary>
    /// Normalizes Nager.Date DTOs into the shared <see cref="ProviderRow"/> shape used by
    /// the catalog matcher. Regional rows (<c>Global = false</c>) are dropped per the
    /// Phase 6.5 contract — only national entries participate in the calendar. The
    /// display description prefers <c>LocalName</c> (Portuguese) and falls back to
    /// <c>Name</c> (English). <c>SearchText</c> normalizes both fields concatenated so
    /// matchers like <c>"good friday"</c> can match against the English name field.
    /// </summary>
    /// <param name="dtos">Nager.Date response DTOs.</param>
    /// <returns>
    /// A list of normalized provider rows tagged <see cref="HolidaySource.Nager"/>,
    /// with regional rows already filtered out.
    /// </returns>
    private static List<ProviderRow> NormalizeNagerRows(IReadOnlyList<NagerDateHolidayDto> dtos)
    {
        var rows = new List<ProviderRow>(dtos.Count);

        foreach (var dto in dtos)
        {
            // Drop regional rows — only national (global=true) entries participate in the calendar.
            if (dto.Global is false)
                continue;

            if (string.IsNullOrWhiteSpace(dto.Date))
                continue;

            if (DateOnly.TryParse(dto.Date, out var date) is false)
                continue;

            var displayName = !string.IsNullOrWhiteSpace(dto.LocalName)
                ? dto.LocalName!
                : dto.Name ?? string.Empty;

            if (string.IsNullOrWhiteSpace(displayName))
                continue;

            // Match against both localName and English name so matchers like "good friday" can hit.
            var searchText = BrazilianHolidayTextNormalizer.Normalize($"{dto.LocalName} {dto.Name}");

            rows.Add(new ProviderRow(date, displayName, HolidaySource.Nager, searchText));
        }

        return rows;
    }

    /// <summary>
    /// Walks the concept catalog and, for each concept, picks the best provider row
    /// whose <c>SearchText</c> contains any of the concept's name matchers. When more
    /// than one row matches a concept (e.g., Nager returns "Carnaval" rows for both
    /// Monday and Tuesday), the row whose date is closest to the concept's
    /// <c>ExpectedDateForYear</c> wins. Rows that don't match any concept are dropped
    /// silently. Rows that match by name but are further than ±3 days from the expected
    /// date still match — the closest-date tiebreaker doesn't apply a hard cap, so a
    /// provider's authoritative date overrides the expected one.
    /// </summary>
    /// <param name="rows">Normalized provider rows from a single source.</param>
    /// <param name="year">Gregorian year used to compute each concept's expected date.</param>
    /// <param name="includeOptionalFederal">
    /// When <c>false</c>, optional-federal concepts are skipped entirely; provider rows
    /// whose only matching concept is optional-federal are therefore dropped.
    /// </param>
    /// <returns>
    /// A dictionary keyed by <c>ConceptId</c> with the winning row's date, description,
    /// catalog-derived type, and provider source for each claimed concept.
    /// </returns>
    private static Dictionary<string, ProviderClaim> MatchToCatalog(IReadOnlyList<ProviderRow> rows,int year,bool includeOptionalFederal)
    {
        var claims = new Dictionary<string, ProviderClaim>();

        foreach (var concept in BrazilianHolidayConceptCatalog.All)
        {
            if (!includeOptionalFederal && concept.Type != BrazilianHolidayType.National)
                continue;

            var expected = concept.ExpectedDateForYear(year);

            ProviderRow? best = null;
            var bestDistance = int.MaxValue;

            foreach (var row in rows)
            {
                if (concept.NameMatchers.Any(matcher => row.SearchText.Contains(matcher)) is false)
                    continue;

                var distance = Math.Abs(row.Date.DayNumber - expected.DayNumber);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = row;
                }
            }

            if (best is not null)
            {
                claims[concept.ConceptId] = new ProviderClaim(best.Date,best.Description,concept.Type,best.Source);
            }
        }

        return claims;
    }

    /// <summary>
    /// Materializes the final ordered list by walking the catalog: for each concept that
    /// has a claim, emits the provider's date/description/source; for each concept
    /// without a claim, falls back to the canonical date/description tagged
    /// <see cref="HolidaySource.Canonical"/>. Optional-federal concepts are filtered out
    /// when <paramref name="includeOptionalFederal"/> is <c>false</c>.
    /// </summary>
    /// <param name="year">Gregorian year used to compute canonical backfill dates.</param>
    /// <param name="includeOptionalFederal">Whether to include the three optional-federal entries.</param>
    /// <param name="claims">Concept claims produced by <see cref="MatchToCatalog"/>.</param>
    /// <returns>
    /// The final list of <see cref="SourcedBrazilianHolidayEntry"/>, ordered ascending by
    /// <c>Date</c> and then by <c>Type</c>.
    /// </returns>
    private static List<SourcedBrazilianHolidayEntry> BuildFromClaims(int year,bool includeOptionalFederal,Dictionary<string, ProviderClaim> claims)
    {
        var entries = new List<SourcedBrazilianHolidayEntry>(BrazilianHolidayConceptCatalog.All.Count);

        foreach (var concept in BrazilianHolidayConceptCatalog.All)
        {
            if (includeOptionalFederal is false && concept.Type != BrazilianHolidayType.National)
                continue;

            entries.Add(claims.TryGetValue(concept.ConceptId, out var claim) ? 
                new SourcedBrazilianHolidayEntry(claim.Date, claim.Description, claim.Type, claim.Source) : 
                new SourcedBrazilianHolidayEntry(concept.ExpectedDateForYear(year), concept.CanonicalDescription, concept.Type, HolidaySource.Canonical));
        }

        return entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Type)
            .ToList();
    }

    /// <summary>
    /// Internal projection of a single provider DTO into the shape the catalog matcher
    /// consumes. <c>SearchText</c> is the accent-stripped lowercased name (or
    /// localName + name, for Nager) used for substring matching against the concept's
    /// <c>NameMatchers</c>; <c>Description</c> is the human-readable label that survives
    /// onto the final response item.
    /// </summary>
    private sealed record ProviderRow(DateOnly Date,string Description,HolidaySource Source,string SearchText);

    /// <summary>
    /// A concept's winning claim, after name and date-proximity matching collapses
    /// candidate provider rows down to a single entry per <c>ConceptId</c>.
    /// </summary>
    private sealed record ProviderClaim(DateOnly Date,string Description,BrazilianHolidayType Type,HolidaySource Source);
}
