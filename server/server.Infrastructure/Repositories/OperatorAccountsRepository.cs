using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class OperatorAccountsRepository(ServerDbContext dbContext) : IOperatorAccountsRepository
{
    public async Task Add(OperatorAccount operatorAccount)
    {
        await dbContext.OperatorAccounts.AddAsync(operatorAccount);
    }

    public async Task<OperatorAccount?> GetByOperatorIdAndAccountId(Guid operatorId, Guid accountId)
    {
        return await dbContext.OperatorAccounts
            .FirstOrDefaultAsync(oa =>
                oa.OperatorId == operatorId &&
                oa.AccountId == accountId);
    }

    public async Task<OperatorAccount?> GetActivePrimaryByOperatorId(Guid operatorId)
    {
        return await dbContext.OperatorAccounts
            .FirstOrDefaultAsync(oa =>
                oa.OperatorId == operatorId &&
                oa.Active &&
                oa.IsPrimary);
    }

    public async Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorId(Guid operatorId)
    {
        return await dbContext.OperatorAccounts
            .AsNoTracking()
            .Where(oa => oa.OperatorId == operatorId && oa.Active)
            .OrderBy(oa => oa.IsPrimary ? 0 : 1)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<OperatorAccount>> ListActiveByOperatorIdWithAccount(Guid operatorId)
    {
        return await dbContext.OperatorAccounts
            .AsNoTracking()
            .Include(oa => oa.Account)
            .Where(oa => oa.OperatorId == operatorId && oa.Active)
            .OrderBy(oa => oa.IsPrimary ? 0 : 1)
            .ThenBy(oa => oa.Account.Name)
            .ToListAsync();
    }
}
