namespace server.Communication.Responses;

public class ResponseCreateHolidaysJson
{
    public IReadOnlyList<ResponseHolidayJson> Items { get; init; } = [];
}
