namespace server.Communication.Requests;

public class RequestOpenChequeAgingJson
{
    public Guid? AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? AsOfDate { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
