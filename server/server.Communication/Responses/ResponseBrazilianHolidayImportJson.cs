using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseBrazilianHolidayImportJson
{
    public int Year { get; init; }
    public bool IncludesOptionalFederal { get; init; }
    public BrazilianHolidayCalendarSource Source { get; init; }
    public IReadOnlyList<ResponseBrazilianHolidayImportItemJson> Items { get; init; } = [];
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
}
