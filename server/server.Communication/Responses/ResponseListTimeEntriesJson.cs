namespace server.Communication.Responses;

public class ResponseListTimeEntriesJson
{
    public IReadOnlyList<ResponseListTimeEntryItemJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasNext { get; init; }
    public bool HasPrevious { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
