namespace server.Communication.Requests;

public class RequestListHolidaysJson
{
    public int? Year { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
