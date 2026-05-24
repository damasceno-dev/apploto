using server.Domain.Entities.Enums;

namespace server.Application.Services.Holidays;

public sealed record SourcedBrazilianHolidayEntry(DateOnly Date,string Description,BrazilianHolidayType Type,HolidaySource Source);
