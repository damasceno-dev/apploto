namespace server.Communication.Requests;

/// <summary>
/// <c>POST /transaction-type/{id}/reclassify</c> (M9.7 Phase 1.3): apply the type's current markers to the exact
/// posted rows of an open window that the candidates read listed, at the versions read. Locked periods never change.
/// </summary>
public class RequestReclassifyTransactionTypeJson
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public string Reason { get; init; } = string.Empty;
    public List<RequestReclassifyRowJson> Rows { get; init; } = [];
}

public class RequestReclassifyRowJson
{
    public Guid TransactionId { get; init; }
    public uint Version { get; init; }
}
