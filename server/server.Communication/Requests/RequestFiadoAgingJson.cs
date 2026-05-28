namespace server.Communication.Requests;

public class RequestFiadoAgingJson
{
    public Guid? ClientId { get; init; }
    public Guid? AccountId { get; init; }
    public DateTime? AsOfDate { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
