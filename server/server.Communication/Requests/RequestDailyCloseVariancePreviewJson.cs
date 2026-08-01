namespace server.Communication.Requests;

public class RequestDailyCloseVariancePreviewJson
{
    public IReadOnlyList<RequestUpsertDailyCloseItemJson>? Items { get; init; }
}
