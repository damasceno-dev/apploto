namespace server.Communication.Responses;

public class ResponseClientBalanceDeltaJson
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal OutstandingDelta { get; init; }
}
