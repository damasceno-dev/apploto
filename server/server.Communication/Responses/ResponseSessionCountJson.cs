using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Blind-count state of a session (§6.20, M9.6 Phase 4). The server issues <see cref="ConfirmationToken"/> and decides
/// <see cref="CandidateState"/>; the expected closing and the difference appear only in an immutable attempt the caller
/// confirmed (<see cref="ConfirmedAttempt"/>) or in the separately labelled <see cref="UnconfirmedRecalculation"/>.
/// </summary>
public class ResponseSessionCountJson
{
    /// <summary>The token the next Conferir must send; rotated by every count, opening or ledger change and by Recontar.</summary>
    public Guid ConfirmationToken { get; init; }
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }
    public SessionCandidateState CandidateState { get; init; }

    /// <summary>
    /// True when the count is unchanged since the latest attempt but the ledger or inherited opening changed: the next
    /// Conferir must send <c>ConfirmCountStillObserved = true</c> (or the operator starts Recontar).
    /// </summary>
    public bool RequiresStillObservedConfirmation { get; init; }

    public Guid? LatestAttemptId { get; init; }
    public int? LatestAttemptNumber { get; init; }

    /// <summary>The attempt whose calculation the latest submission made official; null while never submitted.</summary>
    public Guid? SubmittedAttemptId { get; init; }

    /// <summary>The latest immutable attempt, only while it is <c>Current</c> or <c>Unconfirmed</c>; otherwise null.</summary>
    public ResponseSessionCountAttemptJson? ConfirmedAttempt { get; init; }

    /// <summary>Present only while <c>Unconfirmed</c>: the current revisions applied to the confirmed count. Never official.</summary>
    public ResponseSessionUnconfirmedRecalculationJson? UnconfirmedRecalculation { get; init; }
}

public class ResponseSessionUnconfirmedRecalculationJson
{
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }
    public decimal TotalOpening { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal ExpectedClosing { get; init; }
    public decimal TotalClosing { get; init; }
    public decimal Difference { get; init; }
}

/// <summary>One immutable Conferir: the count it confirmed, the revisions it used and the server calculation.</summary>
public class ResponseSessionCountAttemptJson
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public int AttemptNumber { get; init; }
    public SessionCountAttemptKind Kind { get; init; }
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }
    public decimal TotalOpening { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }
    public decimal ExpectedClosing { get; init; }
    public decimal TotalClosing { get; init; }
    public decimal Difference { get; init; }
    public Guid ConferredByUserId { get; init; }
    public string ConferredByName { get; init; } = string.Empty;
    public Guid? ConferredByOperatorId { get; init; }
    public DateTime ConferredAt { get; init; }
    public IReadOnlyList<ResponseSessionCountAttemptItemJson> Items { get; init; } = [];
}

public class ResponseSessionCountAttemptItemJson
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal? OpeningValue { get; init; }
    public decimal? CountedValue { get; init; }
}

public class ResponseConferSessionCountJson
{
    /// <summary>The immutable attempt this token produced — the same one on every retry of the token.</summary>
    public ResponseSessionCountAttemptJson Attempt { get; init; } = null!;

    /// <summary>True when the token was already conferred and this response returns that earlier attempt.</summary>
    public bool Replayed { get; init; }

    /// <summary>The session as it stands now; its <c>Count.CandidateState</c> says whether the attempt is still current.</summary>
    public ResponseDailyCloseJson Session { get; init; } = null!;
}

public class ResponseApproveSessionBatchJson
{
    public IReadOnlyList<ResponseDailyCloseJson> Sessions { get; init; } = [];
}

/// <summary>What the caller may do on a session now, from current authority and state (never from the UI role alone).</summary>
public class ResponseSessionAllowedActionsJson
{
    public bool CanSaveCount { get; init; }
    public bool CanConferir { get; init; }
    public bool CanRecount { get; init; }
    public bool CanSubmit { get; init; }
    public bool CanRecall { get; init; }
    public bool CanApprove { get; init; }

    /// <summary>True when only <c>Aprovar como único gestor</c> (reason required) can approve this session.</summary>
    public bool CanApproveAsSingleManager { get; init; }

    /// <summary>Why ordinary approval is unavailable to this caller, verbatim from the server; null when it is available.</summary>
    public string? ApprovalBlockedReason { get; init; }
    public bool CanReject { get; init; }
    public bool CanReopen { get; init; }
}
