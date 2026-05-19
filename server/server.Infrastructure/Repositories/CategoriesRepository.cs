using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class CategoriesRepository(ServerDbContext dbContext) : ICategoriesRepository
{
    public async Task AddRange(IEnumerable<Category> categories)
    {
        await dbContext.Categories.AddRangeAsync(categories);
    }

    public async Task Add(Category category)
    {
        await dbContext.Categories.AddAsync(category);
    }

    public async Task<Category?> GetActiveByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.BranchId == branchId && c.Active);
    }

    public async Task<Category?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && c.BranchId == branchId && c.Active);
    }

    public async Task<Category?> GetActiveByIdAndBranchIdWithActiveTransactionTypes(Guid id, Guid branchId)
    {
        return await dbContext.Categories
            .Include(c => c.TransactionTypes.Where(tt => tt.Active))
            .FirstOrDefaultAsync(c => c.Id == id && c.BranchId == branchId && c.Active);
    }

    public async Task<IReadOnlyList<Category>> ListActiveByBranchIdAsNoTracking(Guid branchId)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Where(c => c.BranchId == branchId && c.Active)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsActiveByBranchIdAndName(Guid branchId, string name, Guid? exceptCategoryId = null)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(c =>
                c.BranchId == branchId &&
                c.Name == name &&
                c.Active &&
                (exceptCategoryId == null || c.Id != exceptCategoryId.Value));
    }
}
