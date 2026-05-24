namespace server.Domain.Interfaces.Holidays;

public sealed record BrasilApiHolidayDto(
    string? Date,
    string? Name,
    string? Type,
    string? Weekday);
