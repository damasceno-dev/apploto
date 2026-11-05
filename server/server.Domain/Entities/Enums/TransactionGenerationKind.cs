namespace server.Domain.Entities.Enums;

/// <summary>
/// Why the system generated a transaction (§3.12). A generated row is never written by a request: it is the protected
/// cashbox leg of a fiado operation (M7.7 Phase 5.6, M9.6 Phase 6), the session effect of a close-form field (M9.6
/// Phase 7), the cofre leg of a cashbox → cofre transfer (M9.6 Phase 9) or the fiado debt of a cheque conversion
/// (M9.7 Phase 2).
/// </summary>
public enum TransactionGenerationKind
{
    /// <summary>Cashbox <c>Out</c>: a new counter sale on fiado whose expected cash the drawer never received.</summary>
    FiadoSale = 1,

    /// <summary>Cashbox <c>In</c>: cash received into the paired cashbox's counted drawer on a fiado repayment.</summary>
    FiadoCashRepayment = 2,

    /// <summary>Session effect of <i>Relatório do terminal</i>: <c>In</c> when positive, <c>Out</c> when negative.</summary>
    SessionTerminalReport = 3,

    /// <summary>Session effect of <i>Tarifa Bolão</i> (<c>In</c>).</summary>
    SessionBolaoFee = 4,

    /// <summary>Session effect of <i>Tarifa Especial</i> (<c>In</c>).</summary>
    SessionSpecialFee = 5,

    /// <summary>Cofre <c>In</c> of a cash transfer from a cashbox (<i>Depósito Dinheiro</i>).</summary>
    SafeCashDeposit = 6,

    /// <summary>Tab <c>Out</c>: the fiado debt opened by converting a cheque (M9.7 Phase 2.3); its source is the cheque row.</summary>
    ChequeFiadoConversion = 7,

    /// <summary>Protected typed bank movement or its cofre source; linked by BankMovement.</summary>
    BankMovement = 8
}
