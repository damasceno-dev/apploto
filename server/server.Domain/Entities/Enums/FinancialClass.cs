namespace server.Domain.Entities.Enums;

/// <summary>
/// The five financial classes of the canonical plan (M9.7 C6, frozen at posting by C1). Explicit configuration on the
/// transaction type, never inferred from a name; a system type's class is fixed by its system role.
/// </summary>
public enum FinancialClass
{
    /// <summary>Not classified yet; the default until an admin (or M9.7 Phase 6) classifies the type.</summary>
    Unclassified = 0,

    /// <summary>CAIXA and client money handled.</summary>
    CaixaAndClientMoney = 1,

    /// <summary>The shop's own fees and income (Tarifa Bolão, Tarifa Especial…).</summary>
    ShopFeesAndIncome = 2,

    /// <summary>Operating expenses and unsold-product losses.</summary>
    OperatingExpenseAndLoss = 3,

    /// <summary>Internal transfers (cashbox → cofre, cofre → bank…): never income or expense.</summary>
    InternalTransfer = 4,

    /// <summary>Cash differences.</summary>
    CashDifference = 5
}
