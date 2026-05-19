using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ICategoriesRepository
{
    Task AddRange(IEnumerable<Category> categories);
    Task Add(Category category);
    Task<Category?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Category?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<Category?> GetActiveByIdAndBranchIdWithActiveTransactionTypes(Guid id, Guid branchId);
    Task<IReadOnlyList<Category>> ListActiveByBranchIdAsNoTracking(Guid branchId);
    Task<bool> ExistsActiveByBranchIdAndName(Guid branchId, string name, Guid? exceptCategoryId = null);
}
