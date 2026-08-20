using server.Domain.Entities;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IDailyClosesRepository
{
    Task Add(DailyClose dailyClose, CancellationToken ct = default);
    Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<DailyClose?> GetByBranchIdAndAccountIdAndDateAsNoTracking(Guid branchId, Guid accountId, DateTime date, CancellationToken ct = default);
    Task<DailyClose?> GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime beforeDate,
        CancellationToken ct = default);
    Task<DailyClose?> GetNextEligibleAfterDateByBranchIdAndAccountId(
        Guid branchId,
        Guid accountId,
        DateTime afterDate,
        CancellationToken ct = default);
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
    /// All active closes for the branch on one date (every status, Draft included so the dashboard can
    /// deep-link not-submitted accounts), projected with account and submitting-operator identity.
    /// Ordered AccountName ASC, then Id ASC.
    /// </summary>
    Task<IReadOnlyList<DashboardCloseRow>> ListDashboardClosesByBranchIdAndDateAsNoTracking(
        Guid branchId, DateTime date, CancellationToken ct = default);
    Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(
        Guid branchId, DailyCloseListFilter filter, CancellationToken ct = default);
    Task<int> CountByBranchIdAsNoTracking(
        Guid branchId, DailyCloseListFilter filter, CancellationToken ct = default);
}
