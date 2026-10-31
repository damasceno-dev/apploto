namespace server.Domain.Entities.Enums;

/// <summary>
/// A system-owned transaction type (M7.7 Phase 5.6, M9.6 Phases 6, 7 and 9). System types exist so generated and
/// protected rows have a classification; they are never offered for a generic manual create and cannot be edited or
/// deactivated.
/// </summary>
public enum TransactionTypeSystemRole
{
    /// <summary><i>Saída de caixa — venda fiado</i> (cashbox <c>Out</c> of a new counter fiado sale).</summary>
    FiadoSaleCounterpart = 1,

    /// <summary><i>Entrada de dinheiro — recebimento fiado</i> (cashbox <c>In</c>).</summary>
    FiadoCashRepaymentCounterpart = 2,

    /// <summary><i>Relatório do terminal</i> (cashbox <c>In</c>, positive report).</summary>
    TerminalReportIn = 3,

    /// <summary><i>Relatório do terminal (negativo)</i> (cashbox <c>Out</c>, negative report's magnitude).</summary>
    TerminalReportOut = 4,

    /// <summary><i>Tarifa Bolão</i> (cashbox <c>In</c>); carries <see cref="MonthlyFinancialRole.BolaoServiceFee"/>.</summary>
    BolaoFee = 5,

    /// <summary><i>Tarifa Especial</i> (cashbox <c>In</c>); carries <see cref="MonthlyFinancialRole.SpecialDrawServiceFee"/>.</summary>
    SpecialFee = 6,

    /// <summary><i>Depósito Dinheiro</i>: the explicit cashbox <c>Out</c> of a cashbox → cofre transfer.</summary>
    SafeDepositOut = 7,

    /// <summary><i>Entrada no cofre — depósito do caixa</i>: the generated cofre <c>In</c> of that transfer.</summary>
    SafeDepositIn = 8
}
