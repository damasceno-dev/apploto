using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBrazilianHolidayImportItemJson
{
    public DateOnly Date { get; init; }
    public string Description { get; init; } = string.Empty;
    public BrazilianHolidayType Type { get; init; }
    public BrazilianHolidayImportStatus Status { get; init; }
}
