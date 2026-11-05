using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>Typed posted movement; a pending receipt verifies its existing bank leg, never creates a second one.</summary>
public class BankMovement : EntityBase
{
    public Guid BranchId { get; init; }
    public BankMovementKind Kind { get; init; }
    public Guid BankReceiptId { get; init; }
    public BankReceipt BankReceipt { get; init; } = null!;
    public Guid BankTransactionId { get; init; }
    public Transaction BankTransaction { get; init; } = null!;
    public Guid? SourceTransactionId { get; init; }
    public Transaction? SourceTransaction { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
}
