namespace server.Communication.Requests;

public class RequestCancelTransactionJson
{
    public string CancellationReason { get; init; } = string.Empty;
}
