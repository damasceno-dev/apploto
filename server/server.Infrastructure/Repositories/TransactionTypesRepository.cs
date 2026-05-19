using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TransactionTypesRepository(ServerDbContext dbContext) : ITransactionTypesRepository
{
    public async Task AddRange(IEnumerable<TransactionType> transactionTypes)
    {
        await dbContext.TransactionTypes.AddRangeAsync(transactionTypes);
    }

    public async Task Add(TransactionType transactionType)
    {
        await dbContext.TransactionTypes.AddAsync(transactionType);
    }

    public async Task<TransactionType?> GetActiveByIdWithCategoryAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .FirstOrDefaultAsync(tt =>
                tt.Active &&
                tt.Id == id &&
                tt.Category.BranchId == branchId);
    }

    public async Task<TransactionType?> GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(tt =>
                tt.Active &&
                tt.Id == id &&
                tt.Category.BranchId == branchId);
    }

    public async Task<TransactionType?> GetActiveByIdAndBranchIdWithCategoryAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(transactionType => transactionType.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(transactionType =>
                transactionType.Active &&
                transactionType.Id == id &&
                transactionType.Category.BranchId == branchId);
    }

    public async Task<IReadOnlyList<TransactionType>> ListActiveByBranchIdAsNoTracking(Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .AsNoTracking()
            .Where(tt => tt.Active && tt.Category.BranchId == branchId)
            .OrderBy(tt => tt.Category.Name)
            .ThenBy(tt => tt.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsActiveByCategoryIdAndName(Guid categoryId, string name, Guid? exceptId = null)
    {
        var query = dbContext.TransactionTypes
            .Where(tt => tt.Active && tt.CategoryId == categoryId && tt.Name == name);

        if (exceptId.HasValue)
            query = query.Where(tt => tt.Id != exceptId.Value);

        return await query.AnyAsync();
    }
}
