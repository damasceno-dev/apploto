namespace server.Domain.Interfaces.Holidays;

public sealed record NagerDateHolidayDto(
    string? Date,
    string? LocalName,
    string? Name,
    string? CountryCode,
    bool? Fixed,
    bool? Global,
    IReadOnlyList<string>? Counties,
    int? LaunchYear,
    IReadOnlyList<string>? Types);
