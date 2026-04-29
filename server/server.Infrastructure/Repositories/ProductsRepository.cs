using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class ProductsRepository(ServerDbContext dbContext) : IProductsRepository
{
    public async Task AddRange(IEnumerable<Product> products)
    {
        await dbContext.Products.AddRangeAsync(products);
    }

    public async Task<IReadOnlyList<Product>> ListActiveByIdsAndBranchIdAsNoTracking(
        IEnumerable<Guid> productIds,
        Guid branchId)
    {
        var idList = productIds.ToList();
        return await dbContext.Products
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Active && idList.Contains(p.Id))
            .ToListAsync();
    }

    public async Task<Product?> GetActiveByBranchIdAndNameAsNoTracking(Guid branchId, string name)
    {
        return await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.Active && p.Name == name);
    }
}
