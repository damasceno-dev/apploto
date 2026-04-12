using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class AccountsRepository(ServerDbContext dbContext) : IAccountsRepository
{
    public async Task Add(Account account)
    {
        await dbContext.Accounts.AddAsync(account);
    }

    public async Task<Account?> GetActiveByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Accounts
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                a.BranchId == branchId &&
                a.Active &&
                a.Branch.Active);
    }

    public async Task<Account?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.Id == id &&
                a.BranchId == branchId &&
                a.Active &&
                a.Branch.Active);
    }

    public async Task<IReadOnlyList<Account>> ListActiveByBranchId(Guid branchId)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.Active &&
                a.Branch.Active)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsActiveTerminalForTabAccount(Guid tabAccountId, Guid? excludeAccountId)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .AnyAsync(a =>
                a.TabAccountId == tabAccountId &&
                a.Type == AccountType.Terminal &&
                a.Active &&
                (excludeAccountId == null || a.Id != excludeAccountId));
    }
}
