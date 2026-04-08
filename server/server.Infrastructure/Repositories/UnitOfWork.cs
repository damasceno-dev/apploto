using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class UnitOfWork(ServerDbContext dbContext)
    : IUnitOfWork
{
    public async Task Commit()
    {
        await dbContext.SaveChangesAsync();
    }
}