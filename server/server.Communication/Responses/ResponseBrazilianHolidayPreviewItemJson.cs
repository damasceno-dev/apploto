using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBrazilianHolidayPreviewItemJson
{
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public BrazilianHolidayType Type { get; init; }
    public bool AlreadyExists { get; init; }
    public HolidaySource Source { get; init; }
}
