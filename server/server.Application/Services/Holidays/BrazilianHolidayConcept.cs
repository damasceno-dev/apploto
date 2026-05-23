using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

public sealed record BrazilianHolidayConcept(
    string ConceptId,
    string CanonicalDescription,
    BrazilianHolidayType Type,
    Func<int, DateOnly> ExpectedDateForYear,
    IReadOnlyList<string> NameMatchers);
