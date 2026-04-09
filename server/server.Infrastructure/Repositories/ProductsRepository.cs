using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class ProductsRepository(ServerDbContext dbContext) : IProductsRepository
{
    public async Task AddRange(IEnumerable<Product> products)
    {
        await dbContext.Products.AddRangeAsync(products);
    }
}
