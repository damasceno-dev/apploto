using server.Domain.Entities;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IDailyClosesRepository
{
    Task Add(DailyClose dailyClose);
    Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId);
    Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<DailyClose?> GetByBranchIdAndAccountIdAndDateAsNoTracking(Guid branchId, Guid accountId, DateTime date, CancellationToken ct = default);
    Task<DailyClose?> GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime beforeDate);
    /// <summary>
    /// Active closes for the branch in the given year/month, eager-loading <c>Account</c> (for the name and
    /// ordering). Ordered Date ASC, then AccountName ASC. Powers the monthly reconciliation report; does not
    /// load <c>SubmittedByOperator</c> because that report exposes no operator field.
    /// </summary>
    Task<IReadOnlyList<DailyClose>> ListByBranchIdAndYearMonthAsNoTracking(Guid branchId, int year, int month);
    /// <summary>
    /// All active closes for the branch on one date (every status, Draft included so the dashboard can
    /// deep-link not-submitted accounts), projected with account and submitting-operator identity.
    /// Ordered AccountName ASC, then Id ASC.
    /// </summary>
    Task<IReadOnlyList<DashboardCloseRow>> ListDashboardClosesByBranchIdAndDateAsNoTracking(Guid branchId, DateTime date);
    Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter);
}
