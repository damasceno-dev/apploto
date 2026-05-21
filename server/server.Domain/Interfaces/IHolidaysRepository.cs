using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface IHolidaysRepository
{
    Task Add(Holiday holiday);
    Task<Holiday?> GetByIdAndBranchId(Guid id, Guid branchId);
    Task<Holiday?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<Holiday?> GetActiveByBranchIdAndDate(Guid branchId, DateTime date);
    Task<IReadOnlyList<Holiday>> ListByBranchIdAsNoTracking(Guid branchId, HolidayListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, HolidayListFilter filter);
    Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAsNoTracking(Guid branchId);
    Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAndYearAsNoTracking(Guid branchId, int year);
}
