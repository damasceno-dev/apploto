using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

public sealed record BrazilianHolidayEntry(DateOnly Date, string Description, BrazilianHolidayType Type)
{
    public static BrazilianHolidayEntry National(int year, int month, int day, string description)
    {
        return National(new DateOnly(year, month, day), description);
    }

    public static BrazilianHolidayEntry National(DateOnly date, string description)
    {
        return new BrazilianHolidayEntry(date, description, BrazilianHolidayType.National);
    }

    public static BrazilianHolidayEntry OptionalFederal(int year, int month, int day, string description)
    {
        return OptionalFederal(new DateOnly(year, month, day), description);
    }

    public static BrazilianHolidayEntry OptionalFederal(DateOnly date, string description)
    {
        return new BrazilianHolidayEntry(date, description, BrazilianHolidayType.OptionalFederal);
    }
}
