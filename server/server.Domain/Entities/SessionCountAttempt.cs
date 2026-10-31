using server.Domain.Entities.Enums;

namespace server.Domain.Entities;

/// <summary>
/// One immutable Conferir (§3.30, M9.6 Phase 4): the count it confirmed, the revisions it used and the authoritative
/// server calculation. A later change never overwrites it; a new attempt is appended instead.
/// </summary>
public class SessionCountAttempt : EntityBase
{
    public Guid BranchId { get; init; }

    public Guid SessionId { get; init; }
    public DailyClose Session { get; init; } = null!;

    public int AttemptNumber { get; init; }
    public Guid ConfirmationToken { get; init; }
    public SessionCountAttemptKind Kind { get; init; }

    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }

    public decimal TotalOpening { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal ExpectedClosing { get; init; }
    public decimal TotalClosing { get; init; }

    /// <summary>The §6.12 CashVariance this attempt revealed: TotalClosing − ExpectedClosing.</summary>
    public decimal Difference { get; init; }

    public Guid ConferredByUserId { get; init; }
    public string ConferredByName { get; init; } = string.Empty;
    public Guid? ConferredByOperatorId { get; init; }

    /// <summary>The Idempotency-Key of the request that created the attempt (replays may use other keys).</summary>
    public string CommandKey { get; init; } = string.Empty;

    public ICollection<SessionCountAttemptItem> Items { get; init; } = [];
}
