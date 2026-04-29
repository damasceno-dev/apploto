using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface IDailyClosesRepository
{
    Task Add(DailyClose dailyClose);
    Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId);
    Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<DailyClose?> GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime beforeDate);
    Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter);
}
