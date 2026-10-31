using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

/// <summary>
/// The durable revisions a Conferir attempt is bound to (§6.20, M9.6 Phase 4). Every change to a session's calculation
/// input — its count, its inherited opening or the Active rows bound to it — bumps the matching revision once per
/// command and rotates the confirmation token in the same transaction. Inputs that leave the §6.12 calculation unchanged
/// (Notes, an identical count, Draft rows, non-financial row fields, a row moving with its session) bump nothing.
/// The caller holds the cashbox coordination; the bump rides on the command's own commit.
/// </summary>
public sealed class SessionRevisionService(IDailyClosesRepository dailyClosesRepository)
{
    /// <summary>
    /// Bumps <see cref="DailyClose.LedgerRevision"/> once for each distinct session among <paramref name="rows"/> whose
    /// calculation contribution changed: rows created Active, finalized, or cancelled while Active. Generated pair legs
    /// pass in the same call, so a source and its counterpart on one session count once.
    /// </summary>
    public async Task RecordLedgerChange(Guid branchId, IEnumerable<Transaction> rows, CancellationToken ct = default)
    {
        var sessionIds = rows
            .Select(row => row.SessionId)
            .OfType<Guid>()
            .Distinct()
            .Order()
            .ToList();

        foreach (var sessionId in sessionIds)
        {
            var session = await dailyClosesRepository.GetRootForRevision(sessionId, branchId, ct)
                ?? throw new InvalidOperationException($"Session {sessionId} of a ledger row was not found.");
            session.LedgerRevision++;
            RotateToken(session);
        }
    }

    /// <summary>A changed count (not Notes, not an identical vector).</summary>
    public static void RecordCountChange(DailyClose session)
    {
        session.CountRevision++;
        RotateToken(session);
    }

    /// <summary>An inherited (unacknowledged) opening whose counted predecessor changed or moved.</summary>
    public static void RecordInheritedOpeningChange(DailyClose session)
    {
        if (session.Opening is not null)
            return;

        session.OpeningRevision++;
        RotateToken(session);
    }

    /// <summary>Recontar: a fresh token even when nothing changed, so the next Conferir is a new attempt.</summary>
    public static void BeginRecount(DailyClose session, Guid previousAttemptId)
    {
        session.RecountStartedForAttemptId = previousAttemptId;
        RotateToken(session);
    }

    /// <summary>True when a row in <paramref name="previousStatus"/> contributed to its session's calculation.</summary>
    public static bool Contributes(TransactionStatus previousStatus) => previousStatus == TransactionStatus.Active;

    private static void RotateToken(DailyClose session)
    {
        session.CurrentConfirmationToken = Guid.NewGuid();
    }
}
