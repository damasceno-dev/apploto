namespace server.Communication.Requests;

public class RequestOfferSessionHandoverJson
{
    public Guid IncomingOperatorId { get; init; }
    public long CountRevision { get; init; }
    public string? Reason { get; init; }
}
