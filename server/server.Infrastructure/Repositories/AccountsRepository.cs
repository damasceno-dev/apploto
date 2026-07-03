using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

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

    public async Task<Guid?> GetActiveTerminalIdByTabAccountId(Guid tabAccountId, Guid branchId)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.Type == AccountType.Terminal &&
                a.TabAccountId == tabAccountId &&
                a.Active &&
                a.Branch.Active)
            .Select(a => (Guid?)a.Id)
            .FirstOrDefaultAsync();
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

    public async Task<IReadOnlyDictionary<Guid, Guid>> ListActiveTerminalIdsByTabAccountIds(
        Guid branchId,
        IReadOnlyCollection<Guid> tabAccountIds)
    {
        if (tabAccountIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        return await dbContext.Accounts
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.Type == AccountType.Terminal &&
                a.TabAccountId.HasValue &&
                tabAccountIds.Contains(a.TabAccountId.Value) &&
                a.Active &&
                a.Branch.Active)
            .ToDictionaryAsync(a => a.TabAccountId!.Value, a => a.Id);
    }

    public async Task<IReadOnlyList<ExpectedCloserRow>> ListExpectedClosersByBranchIdAsNoTracking(Guid branchId)
    {
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(a =>
                a.BranchId == branchId &&
                a.Type == AccountType.Terminal &&
                a.Active &&
                a.Branch.Active &&
                a.OperatorAccounts.Any(link => link.Active && link.Operator.Active))
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Select(a => new ExpectedCloserRow(
                a.Id,
                a.Name,
                a.OperatorAccounts
                    .Where(link => link.Active && link.Operator.Active)
                    .OrderByDescending(link => link.IsPrimary)
                    .ThenBy(link => link.Operator.Name)
                    .ThenBy(link => link.OperatorId)
                    .Select(link => link.OperatorId)
                    .First(),
                a.OperatorAccounts
                    .Where(link => link.Active && link.Operator.Active)
                    .OrderByDescending(link => link.IsPrimary)
                    .ThenBy(link => link.Operator.Name)
                    .ThenBy(link => link.OperatorId)
                    .Select(link => link.Operator.Name)
                    .First()))
            .ToListAsync();
    }
}
