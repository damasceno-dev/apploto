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

    public async Task<TransactionType?> GetActiveByIdAndBranchIdWithCategoryAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .AsNoTracking()
            .Include(transactionType => transactionType.Category)
            .FirstOrDefaultAsync(transactionType =>
                transactionType.Id == id &&
                transactionType.Active &&
                transactionType.Category.BranchId == branchId &&
                transactionType.Category.Active &&
                transactionType.Category.Branch.Active);
    }
}
