using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One receivable lifecycle event with both dates (M9.7 Phases 1–2).</summary>
public class ResponseSettlementEventJson
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public SettlementEventKind Kind { get; init; }
    public decimal Amount { get; init; }

    /// <summary>The event's signed effect on the outstanding amount (−Amount, +Amount or 0).</summary>
    public decimal OutstandingDelta { get; init; }

    public DateTime OccurredOn { get; init; }
    public DateTime PostedOn { get; init; }

    /// <summary>True when the event was posted after the date it happened (a late posting).</summary>
    public bool IsLatePosting { get; init; }

    /// <summary>Calendar days between <see cref="OccurredOn"/> and <see cref="PostedOn"/>.</summary>
    public int PostingLagDays { get; init; }

    /// <summary>True when the event counts at the response's cutoff on its basis.</summary>
    public bool CountsAtCutoff { get; init; }

    public SettlementEventProvenance Provenance { get; init; }

    /// <summary>
    /// What the upgrade could not establish about a reconstructed event, one entry per issue; empty on command events and
    /// on clean reconstructions.
    /// </summary>
    public IReadOnlyList<SettlementMigrationIssue> MigrationIssues { get; init; } = [];

    /// <summary>The legacy instant a reconstructed event came from.</summary>
    public DateTime? SourcePaidAt { get; init; }

    public SettlementInstrument SettlementInstrument { get; init; }
    public Guid? BankReceiptId { get; init; }
    public Guid? ReversesEventId { get; init; }
    public Guid? CustodyFromAccountId { get; init; }
    public Guid? CustodyToAccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? NextExpectedDepositDate { get; init; }
    public string? ReturnReason { get; init; }
    public Guid? ConvertedTransactionId { get; init; }
    public Guid? CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
