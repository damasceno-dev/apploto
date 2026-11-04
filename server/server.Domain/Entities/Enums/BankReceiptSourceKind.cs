namespace server.Domain.Entities.Enums;

/// <summary>The operation that expects money in the bank (card batches and recolhimentos follow in Phases 3–4).</summary>
public enum BankReceiptSourceKind
{
    ChequeDeposit = 1
}
