using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface ITimeEntriesRepository
{
    Task Add(TimeEntry timeEntry);
    Task<TimeEntry?> GetByIdAndBranchId(Guid id, Guid branchId);
    Task<TimeEntry?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<TimeEntry?> GetByBranchIdOperatorIdAndDate(Guid branchId, Guid operatorId, DateTime date);
    Task<IReadOnlyList<TimeEntry>> ListByBranchIdAsNoTracking(Guid branchId, TimeEntryListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, TimeEntryListFilter filter);
    Task<bool> ExistsActiveByBranchIdOperatorIdAndDate(Guid branchId, Guid operatorId, DateTime date);
}
