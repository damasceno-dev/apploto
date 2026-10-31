using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

public class DailyClose : EntityBase
{
    public uint Version { get; set; }
    public DateTime Date { get; set; }
    public DailyCloseStatus Status { get; set; } = DailyCloseStatus.Draft;

    public Guid AccountId { get; init; }
    public Account Account { get; init; } = null!;

    public Guid OpenedByUserId { get; init; }
    public User OpenedByUser { get; init; } = null!;

    public Guid? RecordedByUserId { get; set; }
    public User? RecordedByUser { get; set; }

    public Guid? RecordedByOperatorId { get; set; }
    public Operator? RecordedByOperator { get; set; }

    public Guid? SubmittedByUserId { get; set; }
    public User? SubmittedByUser { get; set; }

    public Guid? SubmittedByOperatorId { get; set; }
    public Operator? SubmittedByOperator { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? ItemsFirstRecordedAt { get; set; }
    public DateTime? OpeningRecheckRequiredAt { get; set; }

    public Guid? OpeningRecheckTriggeredByDailyCloseId { get; set; }
    public DailyClose? OpeningRecheckTriggeredByDailyClose { get; set; }

    public Guid? OpeningRecheckTriggeredByUserId { get; set; }
    public User? OpeningRecheckTriggeredByUser { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public Guid BranchId { get; init; }
    public Branch Branch { get; init; } = null!;

    /// <summary>
    /// True when every review event of this close has been captured since it was opened. Closes that
    /// existed before review events shipped are false: their earlier rejections and opening changes
    /// were never recorded and must be shown as unavailable, never inferred.
    /// </summary>
    public bool ReviewHistoryComplete { get; init; } = true;

    // Session identity (M9.6 Phase 2). DailyClose.Id is the stable SessionId; a Ready session has a custodian,
    // a per-cashbox Sequence and a half-open [StartedAt, EndedAt) interval. Unresolved legacy rows keep these
    // null and are read-only history until a reviewed mapping resolves them.
    public SessionMappingState MappingState { get; set; } = SessionMappingState.Ready;
    public SessionOrigin Origin { get; init; } = SessionOrigin.Started;
    public SessionCustodyState? CustodyState { get; set; }

    public Guid? CustodianOperatorId { get; init; }
    public Operator? CustodianOperator { get; init; }
    public Guid? CustodianUserIdAtStart { get; init; }

    public Guid? TerminalId { get; init; }
    public Terminal? Terminal { get; init; }

    public int? Sequence { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Durable revision of the session's ledger, bumped by every write to its rows.</summary>
    public long LedgerRevision { get; set; }

    /// <summary>Durable revision of the session's count, bumped by every changed count save.</summary>
    public long CountRevision { get; set; }

    /// <summary>
    /// Durable revision of the session's calculation opening. An acknowledged opening is immutable, so only an inherited
    /// (unacknowledged) opening moves it, when its counted predecessor changes (M9.6 Phase 4).
    /// </summary>
    public long OpeningRevision { get; set; }

    /// <summary>
    /// Server-issued Conferir token (§6.20). Rotated with every count, opening or ledger revision change and by
    /// BeginRecount; never chosen by a client. A token admits at most one count attempt.
    /// </summary>
    public Guid CurrentConfirmationToken { get; set; } = Guid.NewGuid();

    /// <summary>AttemptNumber of the latest count attempt (0 before the first Conferir).</summary>
    public int LastAttemptNumber { get; set; }

    /// <summary>The attempt an explicit Recontar started from, cleared by the next Conferir.</summary>
    public Guid? RecountStartedForAttemptId { get; set; }

    /// <summary>The attempt the latest submission confirmed; its calculation is the official snapshot.</summary>
    public Guid? SubmittedAttemptId { get; set; }

    public bool CustodyHistoryComplete { get; init; } = true;

    public SessionOpening? Opening { get; init; }

    public ICollection<DailyCloseItem> Items { get; init; } = [];
}
