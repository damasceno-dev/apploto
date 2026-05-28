namespace server.Communication.Responses;

public class ResponseFiadoClientBalanceItemJson
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal OutstandingTotal { get; init; }
}
