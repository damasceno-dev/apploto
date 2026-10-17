namespace server.Domain.Entities.Enums;

/// <summary>
/// A system-owned transaction type (M7.7 Phase 5.6). System types exist so generated rows have a classification;
/// they are never offered for manual create and cannot be edited or deactivated.
/// </summary>
public enum TransactionTypeSystemRole
{
    /// <summary><i>Saída de estoque contado — venda fiado</i> (Terminal <c>Out</c>).</summary>
    FiadoCountedStockSaleCounterpart = 1,

    /// <summary><i>Entrada de dinheiro — recebimento fiado</i> (Terminal <c>In</c>).</summary>
    FiadoCashRepaymentCounterpart = 2
}
