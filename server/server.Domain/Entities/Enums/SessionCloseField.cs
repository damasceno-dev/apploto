namespace server.Domain.Entities.Enums;

/// <summary>
/// The three close-form fields a session owns (M9.6 Phase 7, §6.19 O08). Each keeps one generated, system-owned
/// ledger effect on the session's cashbox while its value is non-zero.
/// </summary>
public enum SessionCloseField
{
    /// <summary><i>Relatório do terminal</i>: the signed CAIXA report expectation (formerly <i>Operador</i>).</summary>
    TerminalReport = 1,

    /// <summary><i>Tarifa Bolão</i>: the session's bolão service fee, nonnegative.</summary>
    BolaoFee = 2,

    /// <summary><i>Tarifa Especial</i>: the session's special-draw service fee, nonnegative.</summary>
    SpecialFee = 3
}
