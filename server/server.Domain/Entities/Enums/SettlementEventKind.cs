namespace server.Domain.Entities.Enums;

/// <summary>
/// What a receivable lifecycle event records (M9.7 Phases 1–2, §6.24). Every event is appended; none edits the
/// receivable row or an earlier event. Its effect on the outstanding amount is fixed by its kind and persisted as
/// <c>OutstandingDelta</c>: settling kinds subtract their amount, a return adds it back, custody and re-presentation
/// leave it unchanged.
/// </summary>
public enum SettlementEventKind
{
    /// <summary>A pre-M9.7 <c>PaidAt</c> reconstructed by the upgrade (<c>OccurredOn = PostedOn = PaidAt</c>); never a new payment.</summary>
    MigratedSettlement = 1,

    /// <summary>The physical cheque moves into a cofre: custody only, the receivable stays open.</summary>
    CustodyMoved = 2,

    /// <summary><i>Depositar</i>: the cheque is deposited (paid = deposited, product Q2) with a pending bank receipt.</summary>
    ChequeDeposited = 3,

    /// <summary><i>Cheque devolvido</i>: the bank returned the deposited cheque; the claim is open again.</summary>
    ChequeReturned = 4,

    /// <summary><i>Reapresentar</i>: the returned cheque becomes depositable again (a new deposit attempt).</summary>
    ChequeRepresented = 5,

    /// <summary><i>Converter em fiado</i>: the cheque claim closes and the same client's fiado debt opens.</summary>
    ChequeConvertedToFiado = 6
}
