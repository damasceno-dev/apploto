namespace server.Communication.Requests;

public class RequestPutDailyCloseItemsJson
{
    public IReadOnlyList<RequestUpsertDailyCloseItemJson>? Items { get; init; }
    public string? Notes { get; init; }
}
