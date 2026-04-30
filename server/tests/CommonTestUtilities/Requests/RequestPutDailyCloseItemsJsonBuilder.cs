using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestPutDailyCloseItemsJsonBuilder
{
    private IReadOnlyList<RequestUpsertDailyCloseItemJson>? _items =
    [
        new RequestUpsertDailyCloseItemJsonBuilder().Build()
    ];

    public RequestPutDailyCloseItemsJsonBuilder WithItems(
        IReadOnlyList<RequestUpsertDailyCloseItemJson>? items)
    {
        _items = items;
        return this;
    }

    public RequestPutDailyCloseItemsJsonBuilder WithItems(
        params RequestUpsertDailyCloseItemJson[] items)
    {
        _items = items;
        return this;
    }

    public RequestPutDailyCloseItemsJson Build()
    {
        return new RequestPutDailyCloseItemsJson { Items = _items };
    }
}
