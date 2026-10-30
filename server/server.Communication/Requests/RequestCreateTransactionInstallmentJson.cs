namespace server.Communication.Requests;

public class RequestCreateTransactionInstallmentJson
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public string? Description { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public Guid TransactionTypeId { get; init; }
    public Guid AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public IReadOnlyList<RequestCreateTransactionInstallmentItemJson> Installments { get; init; } = [];
    public bool AutoGenerateInstallments { get; init; }
    public DateTime? DueDate { get; init; }
    public int? InstallmentCount { get; init; }
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

public class RequestCreateTransactionInstallmentItemJson
{
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
}
