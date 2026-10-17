using Microsoft.EntityFrameworkCore;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class UnitOfWork(ServerDbContext dbContext)
    : IUnitOfWork
{
    public async Task Commit(CancellationToken ct = default)
    {
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> TryCommitOptimistic(CancellationToken ct = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
