namespace server.Communication.Requests;

/// <summary>
/// <c>POST /cheque/{transactionId}/return</c> — <i>Cheque devolvido</i> (M9.7 Phase 2.3): the bank returned the exact
/// deposit named by <see cref="DepositEventId"/>. The claim reopens and the pending receipt expects that amount less.
/// </summary>
public class RequestChequeReturnJson
{
    public Guid DepositEventId { get; init; }
    public int ExpectedVersion { get; init; }

    /// <summary>When the bank returned it; may lie in a locked month, never before the deposit.</summary>
    public DateTime OccurredOn { get; init; }

    /// <summary>The open business date the return is posted on; omitted means branch-local today.</summary>
    public DateTime? PostedOn { get; init; }

    /// <summary>The identified client who owes the cheque.</summary>
    public Guid ClientId { get; init; }

    /// <summary>When the shop next expects to deposit it.</summary>
    public DateTime NextExpectedDepositDate { get; init; }

    /// <summary>Optional bank return reason (<i>alínea</i>).</summary>
    public string? ReturnReason { get; init; }
}
