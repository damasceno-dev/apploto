namespace server.Application.Services.Holidays;

/// <summary>
/// Internal projection used by <see cref="BrazilianHolidayConceptMatcher"/>: a date,
/// a display description, and a normalized search text (accent-stripped lowercased)
/// derived from one or more provider name fields. The matcher operates on this shape
/// regardless of which provider produced the row, so the matching contract can be
/// exercised directly from the catalog tests without going through the resolver.
/// </summary>
internal sealed record BrazilianHolidayProviderRow(DateOnly Date,string Description,string SearchText);
