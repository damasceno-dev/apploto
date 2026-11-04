namespace server.Communication.Requests;

/// <summary>
/// <c>POST /cheque/custody</c> (M9.7 Phase 2.2): move the listed physical cheques into a cofre. Custody only: every
/// receivable stays open and no cash, bank or session effect is written.
/// </summary>
public class RequestChequeCustodyJson
{
    public Guid SafeAccountId { get; init; }

    /// <summary>When the cheques actually moved; may lie in a locked month.</summary>
    public DateTime OccurredOn { get; init; }

    /// <summary>The open business date the move is posted on; omitted means branch-local today.</summary>
    public DateTime? PostedOn { get; init; }

    public List<RequestChequeOperationItemJson> Items { get; init; } = [];
}
