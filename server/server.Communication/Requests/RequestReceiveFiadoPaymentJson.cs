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
    /// <summary>
    /// Cashbox session binding (M9.6 Phase 2). Omitted: the cashbox's active session. A manager's historical write
    /// names the exact Draft session and a reason. When no session is active, <see cref="OpeningAcknowledgment"/>
    /// starts (or accepts) custody atomically with this operation under the same Idempotency-Key.
    /// </summary>
    public Guid? SessionId { get; init; }
    public string? HistoricalReason { get; init; }
    public RequestSessionOpeningAcknowledgmentJson? OpeningAcknowledgment { get; init; }
}
