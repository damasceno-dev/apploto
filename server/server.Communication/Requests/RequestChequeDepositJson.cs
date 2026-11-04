namespace server.Communication.Requests;

/// <summary>
/// <c>POST /cheque/deposit</c> — <i>Depositar</i> (M9.7 Phase 2.2): deposit the listed cheques in one bank account as
/// one batch. Each cheque gets its deposit event (paid = deposited) and the batch one pending bank receipt; all or none.
/// </summary>
public class RequestChequeDepositJson
{
    public Guid BankAccountId { get; init; }

    /// <summary>The deposit date; may lie in a locked month.</summary>
    public DateTime OccurredOn { get; init; }

    /// <summary>The open business date the deposit is posted on; omitted means branch-local today.</summary>
    public DateTime? PostedOn { get; init; }

    public string? Description { get; init; }

    public List<RequestChequeOperationItemJson> Items { get; init; } = [];
}
