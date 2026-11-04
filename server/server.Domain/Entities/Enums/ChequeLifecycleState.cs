namespace server.Domain.Entities.Enums;

/// <summary>Where a cheque stands at a cutoff, derived from its last event (M9.7 Phase 2).</summary>
public enum ChequeLifecycleState
{
    /// <summary>Recorded and still with the recording cashbox; no event yet.</summary>
    Received = 1,

    /// <summary>In a cofre's custody; still open.</summary>
    InSafe = 2,

    /// <summary>Deposited; the bank receipt is pending.</summary>
    Deposited = 3,

    /// <summary>Returned by the bank; awaiting <i>Reapresentar</i> or <i>Converter em fiado</i>.</summary>
    Returned = 4,

    /// <summary>Re-presented; depositable again.</summary>
    AwaitingRedeposit = 5,

    /// <summary>Closed into the client's fiado debt.</summary>
    ConvertedToFiado = 6,

    /// <summary>Settled by a reconstructed legacy <c>PaidAt</c>; its deposit details were never recorded.</summary>
    SettledBeforeEvents = 7
}
