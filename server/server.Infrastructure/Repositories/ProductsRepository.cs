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

    public async Task Add(Product product)
    {
        await dbContext.Products.AddAsync(product);
    }

    public async Task<Product?> GetActiveByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId && p.Active);
    }

    public async Task<Product?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.BranchId == branchId && p.Active);
    }

    public async Task<IReadOnlyList<Product>> ListActiveByBranchIdAsNoTracking(Guid branchId)
    {
        return await dbContext.Products
            .AsNoTracking()
            .Where(p => p.BranchId == branchId && p.Active)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Id)
            .ToListAsync();
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

    public async Task<bool> ExistsActiveByBranchIdAndName(Guid branchId, string name, Guid? exceptId = null)
    {
        return await dbContext.Products
            .AsNoTracking()
            .AnyAsync(p =>
                p.BranchId == branchId &&
                p.Name == name &&
                p.Active &&
                (exceptId == null || p.Id != exceptId.Value));
    }
}
