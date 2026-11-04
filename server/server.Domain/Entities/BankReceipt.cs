using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// Money the shop expects in a bank account (M9.7 Phase 2.2): one pending receipt per deposit batch, distinct from the
/// customer claims it settles (their deposit events carry <see cref="SettlementEvent.BankReceiptId"/>, which are the
/// receipt's allocations) and from the physical cheques. It is not a ledger row and never claims the money arrived;
/// statement reconciliation (Phase 5) confirms an existing receipt. A later return reduces the expected amount through
/// its own event; the receipt row itself is never edited except for its status.
/// </summary>
public class BankReceipt : EntityBase
{
    public Guid BranchId { get; init; }

    public Guid BankAccountId { get; init; }
    public Account BankAccount { get; init; } = null!;

    public BankReceiptSourceKind SourceKind { get; init; }
    public BankReceiptStatus Status { get; set; } = BankReceiptStatus.Pending;

    public DateTime OccurredOn { get; init; }
    public DateTime PostedOn { get; init; }

    /// <summary>The deposited total: the sum of the batch's deposit events.</summary>
    public decimal Amount { get; init; }

    public string? Description { get; init; }

    public Guid CreatedByUserId { get; init; }

    public ICollection<SettlementEvent> Allocations { get; init; } = [];
}
