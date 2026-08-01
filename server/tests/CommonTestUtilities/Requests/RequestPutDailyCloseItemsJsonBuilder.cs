using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestPutDailyCloseItemsJsonBuilder
{
    private uint _version = 1;
    private string? _notes;
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

    public RequestPutDailyCloseItemsJsonBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public RequestPutDailyCloseItemsJsonBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public RequestPutDailyCloseItemsJson Build()
    {
        return new RequestPutDailyCloseItemsJson
        {
            Version = _version,
            Items = _items,
            Notes = _notes
        };
    }
}
