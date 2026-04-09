using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class CategoriesRepository(ServerDbContext dbContext) : ICategoriesRepository
{
    public async Task AddRange(IEnumerable<Category> categories)
    {
        await dbContext.Categories.AddRangeAsync(categories);
    }
}
