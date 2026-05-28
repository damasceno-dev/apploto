namespace server.Communication.Requests;

public class RequestFiadoBalanceJson
{
    public Guid? ClientId { get; init; }
    public DateTime? AsOfDate { get; init; }
}
