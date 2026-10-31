using server.Domain.Entities;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IDailyClosesRepository
{
    Task Add(DailyClose dailyClose, CancellationToken ct = default);
    Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// The session root alone, tracked, for a revision bump: the instance already tracked by this unit of work (an
    /// added inline start included) when there is one, otherwise loaded. Null when missing or foreign.
    /// </summary>
    Task<DailyClose?> GetRootForRevision(Guid id, Guid branchId, CancellationToken ct = default);
    /// <summary>
    /// The counted session whose closing is the inherited opening of <paramref name="session"/>: the latest Ready
    /// session of the same cashbox with a lower Sequence and a recorded count. An Unresolved legacy row keeps the
    /// shipped date rule (latest earlier counted close of the account).
    /// </summary>
    Task<DailyClose?> GetCountedPredecessorAsNoTracking(DailyClose session, CancellationToken ct = default);

    /// <summary>
    /// The next dependent session of the same cashbox: the first higher-Sequence session that acknowledged an opening
    /// or recorded a count, tracked with its opening; null when none follows.
    /// </summary>
    Task<DailyClose?> GetDependentSuccessor(DailyClose session, CancellationToken ct = default);

    /// <summary>The cashbox's single Active custody session, tracked, or null.</summary>
    Task<DailyClose?> GetActiveCustodyByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default);
    Task<DailyClose?> GetActiveCustodyByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default);

    /// <summary>The latest counted Ready session of the cashbox (proposal source for the next start), with items.</summary>
    Task<DailyClose?> GetLatestCountedReadyByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default);

    Task<int> GetMaxSequenceByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default);

    /// <summary>Every Ready session of the cashbox, tracked, ordered by StartedAt then Sequence (renumbering).</summary>
    Task<IReadOnlyList<DailyClose>> ListReadyByAccountIdOrderedByStart(Guid branchId, Guid accountId, CancellationToken ct = default);

    /// <summary>True when the cashbox still has Unresolved legacy sessions or cashbox rows without a session.</summary>
    Task<bool> HasUnresolvedHistoryByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Earliest business date of an uncounted Ready predecessor of <paramref name="session"/> (lower Sequence, same
    /// cashbox, no recorded count) that carries active financial rows, dated after <paramref name="afterDateExclusive"/>.
    /// </summary>
    Task<DateTime?> GetEarliestUncountedPredecessorActivityDateAsNoTracking(
        DailyClose session,
        DateTime afterDateExclusive,
        CancellationToken ct = default);

    /// <summary>Active custody sessions of the branch with cashbox and custodian names (queue and workstation).</summary>
    Task<IReadOnlyList<DailyClose>> ListActiveCustodyByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Earliest business date carrying an active close in the branch, regardless of workflow status.
    /// Used to keep a fresh branch's month-lock floor from skipping backdated/imported history.
    /// </summary>
    Task<DateTime?> GetEarliestDateByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
    /// <summary>
    /// Active closes for the branch in the given year/month, eager-loading <c>Account</c> (for the name and
    /// ordering). Ordered Date ASC, then AccountName ASC. Powers the monthly reconciliation report; does not
    /// load <c>SubmittedByOperator</c> because that report exposes no operator field.
    /// </summary>
    Task<IReadOnlyList<DailyClose>> ListByBranchIdAndYearMonthAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default);
    /// <summary>
    /// All active closes for the branch on one date (every status, Draft included so the manager queue can
    /// deep-link not-submitted accounts), projected with account and submitting-operator identity.
    /// Ordered AccountName ASC, then Id ASC.
    /// </summary>
    Task<IReadOnlyList<ManagerQueueCloseRow>> ListManagerQueueClosesByBranchIdAndDateAsNoTracking(
        Guid branchId, DateTime date, CancellationToken ct = default);
    /// <summary>
    /// Per-status counts and the earliest date of active Submitted/Rejected/Draft closes strictly between
    /// the two dates (Approved excluded). One grouped aggregate — NOT the paginated list — so the counts
    /// are exact and the oldest date does not depend on list ordering.
    /// </summary>
    Task<ManagerQueueBacklogRow> GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime afterDateExclusive, DateTime beforeDateExclusive, CancellationToken ct = default);
    /// <summary>
    /// Active closes of every status dated in <c>[dateFrom, dateTo]</c>, in the <c>GET /dailyclose</c> order
    /// (<c>Date DESC, CreatedAt DESC, Id DESC</c>), optionally limited to the first <paramref name="take"/> rows, each
    /// with recorder and submitter identities (M11 Phases 6–7).
    /// </summary>
    Task<IReadOnlyList<DailyCloseSummaryRow>> ListCloseSummariesByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime dateFrom, DateTime dateTo, int? take = null, CancellationToken ct = default);

    /// <summary>Active close counts per status over <c>[dateFrom, dateTo]</c> from one grouped query.</summary>
    Task<IReadOnlyList<DailyCloseStatusCountRow>> CountByStatusByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default);

    Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(
        Guid branchId, DailyCloseListFilter filter, CancellationToken ct = default);
    Task<int> CountByBranchIdAsNoTracking(
        Guid branchId, DailyCloseListFilter filter, CancellationToken ct = default);
}
