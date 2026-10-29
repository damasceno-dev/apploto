using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.Services.DailyCloses;

/// <summary>
/// Who writes a new cashbox row and how it names its session (§6.20 RecordSessionOperation): the active session by
/// default; an exact historical session with reason for a manager; or an opening acknowledgment that starts (or
/// accepts) custody atomically with the operation when none is active.
/// </summary>
public sealed record SessionRowContext(
    BranchUser Actor,
    MemberAccountScope Scope,
    Guid? RequestedSessionId = null,
    string? HistoricalReason = null,
    RequestSessionOpeningAcknowledgmentJson? Acknowledgment = null,
    bool Preview = false);

public sealed record SessionRowResolution(DailyClose? Session, SessionStartResult? Started)
{
    public Guid? SessionId => Session?.Id;
    public static SessionRowResolution None { get; } = new(null, null);
}

public interface IDailyCloseLedgerGuard
{
    /// <summary>
    /// Resolves (and when acknowledged, starts) the session a new row of <paramref name="accountId"/> belongs to;
    /// returns <see cref="SessionRowResolution.None"/> for Tab/Bank rows. The caller holds the account coordination.
    /// </summary>
    Task<SessionRowResolution> ResolveSessionForNewRow(
        Guid accountId,
        AccountType accountType,
        DateTime date,
        SessionRowContext context,
        CancellationToken ct = default);

    /// <summary>An existing row's ledger is mutable only while its own session is Draft.</summary>
    Task EnsureLedgerIsMutable(Transaction row, CancellationToken ct = default);

    Task EnsureNoOutstandingDraftTransactions(DailyClose session, CancellationToken ct = default);
}
