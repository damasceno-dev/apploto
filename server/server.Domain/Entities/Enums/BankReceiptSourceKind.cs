namespace server.Domain.Entities.Enums;

/// <summary>The operation whose existing bank expectation statement reconciliation verifies (M9.7 Phases 2–5).</summary>
public enum BankReceiptSourceKind
{
    ChequeDeposit = 1,
    ProviderSettlement = 2,
    SafeCollection = 3,
    CaixaPrizeReimbursement = 4,
    BankFee = 5,
    ChequeReturn = 6
}
