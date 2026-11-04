using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// One append-only lifecycle event of a receivable row (M9.7 Phases 1–2, §6.24). Events form a chain per receivable
/// (<see cref="Sequence"/> 1..n, each pointing at its predecessor); the chain's last sequence is the receivable's
/// version. The receivable row — even in a locked month — is never edited: its status, outstanding amount and cheque
/// state are projected from the events up to a cutoff on either date basis. Rows are immutable in the database.
/// </summary>
public class SettlementEvent : EntityBase
{
    public Guid BranchId { get; init; }

    public Guid ReceivableTransactionId { get; init; }
    public Transaction ReceivableTransaction { get; init; } = null!;

    public int Sequence { get; init; }
    public Guid? PreviousEventId { get; init; }

    public SettlementEventKind Kind { get; init; }

    /// <summary>Always positive: the amount the event concerns.</summary>
    public decimal Amount { get; init; }

    /// <summary>Signed effect on the outstanding amount, fixed by <see cref="Kind"/> (−Amount, +Amount or 0).</summary>
    public decimal OutstandingDelta { get; init; }

    /// <summary>When it actually happened; may lie in a locked period.</summary>
    public DateTime OccurredOn { get; init; }

    /// <summary>When it was posted; an open period at posting time.</summary>
    public DateTime PostedOn { get; init; }

    public SettlementEventProvenance Provenance { get; init; }
    public SettlementMigrationIssue MigrationIssue { get; init; }

    /// <summary>The legacy instant a reconstructed event came from; null on command events.</summary>
    public DateTime? SourcePaidAt { get; init; }

    /// <summary>The receivable's frozen instrument when the event was appended.</summary>
    public SettlementInstrument SettlementInstrument { get; init; }

    public Guid? BankReceiptId { get; init; }
    public BankReceipt? BankReceipt { get; init; }

    /// <summary>A return names the exact deposit event it reverses.</summary>
    public Guid? ReversesEventId { get; init; }

    public Guid? CustodyFromAccountId { get; init; }
    public Guid? CustodyToAccountId { get; init; }

    public Guid? ClientId { get; init; }
    public DateTime? NextExpectedDepositDate { get; init; }

    /// <summary>Optional bank return reason (<i>alínea</i>).</summary>
    public string? ReturnReason { get; init; }

    /// <summary>The fiado debt row a conversion opened.</summary>
    public Guid? ConvertedTransactionId { get; init; }

    /// <summary>The acting user; null only on reconstructed events.</summary>
    public Guid? CreatedByUserId { get; init; }
}
