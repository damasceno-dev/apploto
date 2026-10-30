namespace server.Communication.Requests;

public class RequestRearrangePastSessionJson
{
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public string? Reason { get; init; }
}
