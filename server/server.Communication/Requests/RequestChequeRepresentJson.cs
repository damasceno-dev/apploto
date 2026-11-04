namespace server.Communication.Requests;

/// <summary>
/// <c>POST /cheque/{transactionId}/represent</c> — <i>Reapresentar</i> (M9.7 Phase 2.3): the returned cheque becomes
/// depositable again; the next deposit is a new attempt and earlier deposit/return history stays.
/// </summary>
public class RequestChequeRepresentJson
{
    public int ExpectedVersion { get; init; }
    public DateTime OccurredOn { get; init; }
    public DateTime? PostedOn { get; init; }
}
