namespace server.Communication.Requests;

/// <summary>
/// <i>Depósito Dinheiro</i> (M9.6 Phase 9, §6.19 O09): cash moved from a cashbox to a cofre in one operation — the
/// cashbox <c>Out</c> on its session and the generated cofre <c>In</c>, both or neither. The type is the branch's
/// system <i>Depósito Dinheiro</i>; no other type can record a deposit. An internal transfer, never revenue/expense.
/// </summary>
public class RequestSafeDepositJson
{
    public DateTime Date { get; init; }
    public decimal Value { get; init; }
    public TimeOnly? TransactionTime { get; init; }
    public string? Description { get; init; }

    /// <summary>The source cashbox.</summary>
    public Guid AccountId { get; init; }

    /// <summary>The destination cofre (an active <c>Safe</c> account of the branch).</summary>
    public Guid SafeAccountId { get; init; }
    public Guid? RecordedByOperatorId { get; init; }
    public bool SaveAsDraft { get; init; }

    /// <summary>Cashbox session binding, as on every cashbox write (M9.6 Phase 2).</summary>
    public Guid? SessionId { get; init; }
    public string? HistoricalReason { get; init; }
    public RequestSessionOpeningAcknowledgmentJson? OpeningAcknowledgment { get; init; }
}
