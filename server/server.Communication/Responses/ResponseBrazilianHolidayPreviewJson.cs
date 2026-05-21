namespace server.Communication.Responses;

public class ResponseBrazilianHolidayPreviewJson
{
    public int Year { get; init; }
    public bool IncludesOptionalFederal { get; init; }
    public IReadOnlyList<ResponseBrazilianHolidayPreviewItemJson> Items { get; init; } = [];
}
