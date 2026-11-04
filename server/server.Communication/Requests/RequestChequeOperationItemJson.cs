namespace server.Communication.Requests;

/// <summary>
/// One cheque receivable in a batch command, with the receivable version the client last read (its last event
/// sequence; 0 when it has no event).
/// </summary>
public class RequestChequeOperationItemJson
{
    public Guid TransactionId { get; init; }
    public int ExpectedVersion { get; init; }
}
