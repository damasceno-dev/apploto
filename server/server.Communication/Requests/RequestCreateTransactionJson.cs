namespace server.Communication.Requests;

public class RequestCreateTransactionJson
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public Guid TransactionTypeId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? DueDate { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public bool SaveAsDraft { get; init; }

    /// <summary>
    /// M7.7 Phase 5.6: true only on a fiado sale (Tab <c>Out</c> with a client) that hands the client counted stock;
    /// the server then also writes the generated Terminal <c>Out</c> on the Tab's paired terminal. Never assumed.
    /// </summary>
    public bool HandsOutCountedStock { get; init; }
    /// <summary>
    /// Cashbox session binding (M9.6 Phase 2). Omitted: the cashbox's active session. A manager's historical write
    /// names the exact Draft session and a reason. When no session is active, <see cref="OpeningAcknowledgment"/>
    /// starts (or accepts) custody atomically with this operation under the same Idempotency-Key.
    /// </summary>
    public Guid? SessionId { get; init; }
    public string? HistoricalReason { get; init; }
    public RequestSessionOpeningAcknowledgmentJson? OpeningAcknowledgment { get; init; }
}
