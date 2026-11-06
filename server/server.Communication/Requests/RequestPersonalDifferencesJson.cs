namespace server.Communication.Requests;

public class RequestPersonalDifferencesJson
{
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime? AsOfDate { get; init; }
    public Guid? OperatorId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
