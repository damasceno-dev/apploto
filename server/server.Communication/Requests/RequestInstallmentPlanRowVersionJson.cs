namespace server.Communication.Requests;

public class RequestInstallmentPlanRowVersionJson
{
    public Guid TransactionId { get; init; }
    public uint Version { get; init; }
}
