using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class UnitOfWork : IUnitOfWork
{
    private readonly LotoDbContext _dbContext;

    public UnitOfWork(LotoDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task Commit()
    {
        await _dbContext.SaveChangesAsync();
    }
}