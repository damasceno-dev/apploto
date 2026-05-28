namespace server.Communication.Responses;

public class ResponseFiadoAgingJson
{
    public IReadOnlyList<ResponseFiadoAgingItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public DateTime AsOfDate { get; init; }
}
