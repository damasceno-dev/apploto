namespace server.Domain.Entities.Enums;

/// <summary>
/// A bank receipt's state. M9.7 Phase 2 writes only <c>Pending</c>; statement reconciliation (Phase 5) owns the
/// confirmation of an existing receipt.
/// </summary>
public enum BankReceiptStatus
{
    Pending = 1
}
