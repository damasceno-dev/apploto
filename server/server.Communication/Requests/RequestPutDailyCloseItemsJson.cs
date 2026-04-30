namespace server.Communication.Requests;

public class RequestPutDailyCloseItemsJson
{
    public IReadOnlyList<RequestUpsertDailyCloseItemJson>? Items { get; init; }
}
