using server.Domain.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TransactionTypesRepository(ServerDbContext dbContext) : ITransactionTypesRepository
{
    public async Task AddRange(IEnumerable<TransactionType> transactionTypes)
    {
        await dbContext.TransactionTypes.AddRangeAsync(transactionTypes);
    }

    public async Task Add(TransactionType transactionType)
    {
        await dbContext.TransactionTypes.AddAsync(transactionType);
    }

    public async Task<TransactionType?> GetActiveByIdWithCategoryAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .FirstOrDefaultAsync(tt =>
                tt.Active &&
                tt.Id == id &&
                tt.Category.BranchId == branchId);
    }

    public async Task<TransactionType?> GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(tt =>
                tt.Active &&
                tt.Id == id &&
                tt.Category.BranchId == branchId);
    }

    public async Task<TransactionType?> GetActiveSystemTypeByBranchIdAsNoTracking(
        Guid branchId,
        TransactionTypeSystemRole role,
        CancellationToken ct = default)
    {
        return await dbContext.TransactionTypes
            .Include(transactionType => transactionType.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(transactionType =>
                transactionType.Active &&
                transactionType.SystemRole == role &&
                transactionType.Category.BranchId == branchId,
                ct);
    }

    public async Task<bool> IsSystemCategoryAsNoTracking(Guid categoryId, CancellationToken ct = default)
    {
        // Only the dedicated automatic-movement categories are system categories (M9.6 Phase 7): Receita, Entradas
        // and Saídas also host system types (fees, report, deposit) and stay open for custom types.
        return await dbContext.TransactionTypes
            .AsNoTracking()
            .AnyAsync(transactionType => transactionType.CategoryId == categoryId && (
                transactionType.SystemRole == TransactionTypeSystemRole.FiadoSaleCounterpart ||
                transactionType.SystemRole == TransactionTypeSystemRole.FiadoCashRepaymentCounterpart ||
                transactionType.SystemRole == TransactionTypeSystemRole.SafeDepositIn), ct);
    }

    public async Task<TransactionType?> GetActiveByIdAndBranchIdWithCategoryAsNoTracking(
        Guid id,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.TransactionTypes
            .Include(transactionType => transactionType.Category)
            .AsNoTracking()
            .FirstOrDefaultAsync(transactionType =>
                transactionType.Active &&
                transactionType.Id == id &&
                transactionType.Category.BranchId == branchId,
                ct);
    }

    public async Task<IReadOnlyList<TransactionType>> ListActiveByBranchIdAsNoTracking(Guid branchId)
    {
        return await dbContext.TransactionTypes
            .Include(tt => tt.Category)
            .AsNoTracking()
            .Where(tt => tt.Active && tt.Category.BranchId == branchId)
            .OrderBy(tt => tt.Category.Name)
            .ThenBy(tt => tt.Name)
            .ToListAsync();
    }

    public async Task<bool> ExistsActiveByCategoryIdAndName(Guid categoryId, string name, Guid? exceptId = null)
    {
        var query = dbContext.TransactionTypes
            .Where(tt => tt.Active && tt.CategoryId == categoryId && tt.Name == name);

        if (exceptId.HasValue)
            query = query.Where(tt => tt.Id != exceptId.Value);

        return await query.AnyAsync();
    }

    public Task<bool> ExistsWithSettlementInstrumentByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default)
    {
        return dbContext.TransactionTypes
            .AsNoTracking()
            .AnyAsync(type =>
                type.Category.BranchId == branchId &&
                type.SettlementInstrument != SettlementInstrument.None,
                ct);
    }

    public async Task<IReadOnlyList<MonthlyFinancialRole>> ListConfiguredMonthlyFinancialRolesByBranchIdAsNoTracking(
        Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.TransactionTypes
            .AsNoTracking()
            .Where(type => type.Category.BranchId == branchId && type.MonthlyFinancialRole != MonthlyFinancialRole.None)
            .Select(type => type.MonthlyFinancialRole)
            .Distinct()
            .ToListAsync(ct);
    }
}
