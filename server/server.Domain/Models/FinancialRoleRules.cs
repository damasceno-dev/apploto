using server.Domain.Entities.Enums;

namespace server.Domain.Models;

/// <summary>
/// Fixed relations between system roles, generation kinds, close fields and debt origins (M9.6 Phases 6, 7 and 9).
/// Pure rules shared by the seeds, the commands and the database-facing checks; no configuration reads.
/// </summary>
public static class FinancialRoleRules
{
    /// <summary>
    /// True when the debt origin is a counter sale, whose expected cash the cashbox never received: it always writes
    /// the paired cashbox <c>Out</c>. Opening obligations, imports and cheque conversions never do.
    /// </summary>
    public static bool RequiresCashboxLeg(FiadoDebtOrigin origin) => origin == FiadoDebtOrigin.CounterSale;

    /// <summary>
    /// System roles that own a dedicated system category (the automatic fiado and cofre movements): such a category
    /// classifies only system rows and is read-only. The close-field and deposit roles live in ordinary categories
    /// (Receita, Entradas, Saídas), which stay open for custom types.
    /// </summary>
    public static bool OwnsCategory(TransactionTypeSystemRole role) => role is
        TransactionTypeSystemRole.FiadoSaleCounterpart or
        TransactionTypeSystemRole.FiadoCashRepaymentCounterpart or
        TransactionTypeSystemRole.SafeDepositIn;

    /// <summary>The monthly financial role a system type must carry (only the two fee roles carry one).</summary>
    public static MonthlyFinancialRole MonthlyFinancialRoleOf(TransactionTypeSystemRole role) => role switch
    {
        TransactionTypeSystemRole.BolaoFee => MonthlyFinancialRole.BolaoServiceFee,
        TransactionTypeSystemRole.SpecialFee => MonthlyFinancialRole.SpecialDrawServiceFee,
        _ => MonthlyFinancialRole.None
    };

    /// <summary>
    /// The financial class a system type carries (M9.7 Phase 1.3), fixed by its role from the canonical five-class
    /// plan: the CAIXA report and the fiado legs are CAIXA and client money, the two fees are the shop's own income and
    /// the cofre legs are an internal transfer.
    /// </summary>
    public static FinancialClass FinancialClassOf(TransactionTypeSystemRole role) => role switch
    {
        TransactionTypeSystemRole.FiadoSaleCounterpart or
        TransactionTypeSystemRole.FiadoCashRepaymentCounterpart or
        TransactionTypeSystemRole.TerminalReportIn or
        TransactionTypeSystemRole.TerminalReportOut or
        TransactionTypeSystemRole.ChequeFiadoConversion => FinancialClass.CaixaAndClientMoney,
        TransactionTypeSystemRole.BolaoFee or
        TransactionTypeSystemRole.SpecialFee => FinancialClass.ShopFeesAndIncome,
        TransactionTypeSystemRole.SafeDepositOut or
        TransactionTypeSystemRole.SafeDepositIn => FinancialClass.InternalTransfer,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static TransactionGenerationKind GenerationKindOf(SessionCloseField field) => field switch
    {
        SessionCloseField.TerminalReport => TransactionGenerationKind.SessionTerminalReport,
        SessionCloseField.BolaoFee => TransactionGenerationKind.SessionBolaoFee,
        SessionCloseField.SpecialFee => TransactionGenerationKind.SessionSpecialFee,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };

    /// <summary>The system type of a field's effect for a signed value: a negative report is an <c>Out</c>.</summary>
    public static TransactionTypeSystemRole SystemRoleOf(SessionCloseField field, decimal signedValue) => field switch
    {
        SessionCloseField.TerminalReport => signedValue < 0
            ? TransactionTypeSystemRole.TerminalReportOut
            : TransactionTypeSystemRole.TerminalReportIn,
        SessionCloseField.BolaoFee => TransactionTypeSystemRole.BolaoFee,
        SessionCloseField.SpecialFee => TransactionTypeSystemRole.SpecialFee,
        _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
    };

    /// <summary>True for the roles whose rows only a close-form field writes.</summary>
    public static bool IsSessionFieldRole(TransactionTypeSystemRole role) => role is
        TransactionTypeSystemRole.TerminalReportIn or
        TransactionTypeSystemRole.TerminalReportOut or
        TransactionTypeSystemRole.BolaoFee or
        TransactionTypeSystemRole.SpecialFee;

    /// <summary>
    /// True for the generation kinds that hang off a source transaction (fiado pairs, cofre deposit, and the fiado debt
    /// of a cheque conversion, whose source is the cheque row).
    /// </summary>
    public static bool HasSourceTransaction(TransactionGenerationKind kind) => kind is
        TransactionGenerationKind.FiadoSale or
        TransactionGenerationKind.FiadoCashRepayment or
        TransactionGenerationKind.SafeCashDeposit or
        TransactionGenerationKind.ChequeFiadoConversion;
}
