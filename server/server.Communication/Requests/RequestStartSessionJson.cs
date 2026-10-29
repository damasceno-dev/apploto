namespace server.Communication.Requests;

public class RequestStartSessionJson
{
    public Guid AccountId { get; init; }
    public RequestSessionOpeningAcknowledgmentJson? Opening { get; init; }
}
