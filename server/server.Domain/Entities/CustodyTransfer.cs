using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// A cash transfer from a cashbox to a cofre (M9.6 Phase 9, §3.30 <c>CustodyTransfer</c>): one operation with a stable
/// id and two protected legs — the explicit cashbox <c>Out</c> on its session and the generated cofre <c>In</c> —
/// committed, finalized and cancelled together. An internal transfer: never shop revenue or expense.
/// </summary>
public class CustodyTransfer : EntityBase
{
    public Guid BranchId { get; init; }
    public CustodyTransferAssetKind AssetKind { get; init; }
    public decimal Value { get; init; }

    public Guid SourceAccountId { get; init; }
    public Account SourceAccount { get; init; } = null!;
    public Guid? SourceSessionId { get; init; }
    public Guid SourceTransactionId { get; init; }
    public Transaction SourceTransaction { get; init; } = null!;

    public Guid DestinationAccountId { get; init; }
    public Account DestinationAccount { get; init; } = null!;
    public Guid DestinationTransactionId { get; init; }
    public Transaction DestinationTransaction { get; init; } = null!;

    public Guid CreatedByUserId { get; init; }
}
