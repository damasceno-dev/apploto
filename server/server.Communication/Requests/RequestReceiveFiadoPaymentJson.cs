namespace server.Communication.Requests;

/// <summary>
/// <i>Receber pagamento</i> (M7.7 Phase 5.1): a client's fiado repayment received on an explicit Tab. The method is
/// <see cref="TransactionTypeId"/> — a configured repayment type, required, with no default; a counted-cash method
/// also writes the generated Terminal <c>In</c> on the Tab's paired terminal. Netting stays branch/client-wide, so
/// the Tab only records where the payment came in.
/// </summary>
public class RequestReceiveFiadoPaymentJson
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public string? Description { get; init; }
    public Guid TransactionTypeId { get; init; }
    public Guid AccountId { get; init; }
    public Guid ClientId { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public bool SaveAsDraft { get; init; }
}
