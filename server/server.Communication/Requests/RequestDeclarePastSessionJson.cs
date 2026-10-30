namespace server.Communication.Requests;

public class RequestDeclarePastSessionJson
{
    public Guid AccountId { get; init; }
    public Guid CustodianOperatorId { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public string? Reason { get; init; }
}
