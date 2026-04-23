using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Repositories;

internal class TransactionsRepository(ServerDbContext dbContext) : ITransactionsRepository
{
    public async Task Add(Transaction transaction)
    {
        await dbContext.Transactions.AddAsync(transaction);
    }

    public async Task AddRange(IEnumerable<Transaction> transactions)
    {
        await dbContext.Transactions.AddRangeAsync(transactions);
    }

    public async Task<Transaction?> GetByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Transactions
            .Include(transaction => transaction.TransactionType)
            .ThenInclude(transactionType => transactionType.Category)
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id &&
                transaction.BranchId == branchId);
    }

    public async Task<Transaction?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id &&
                transaction.BranchId == branchId);
    }

    public async Task<IReadOnlyList<Transaction>> ListByBranchIdAsNoTracking(
        Guid branchId,
        TransactionListFilter filter)
    {
        return await ApplyFilter(dbContext.Transactions, branchId, filter)
            .AsNoTracking()
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.TransactionTime.HasValue)
            .ThenByDescending(transaction => transaction.TransactionTime)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter)
    {
        return await ApplyFilter(dbContext.Transactions, branchId, filter)
            .AsNoTracking()
            .CountAsync();
    }

    public async Task<IReadOnlyList<Transaction>> ListByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originId,
        Guid branchId)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.OriginTransactionId == originId &&
                transaction.BranchId == branchId)
            .ToListAsync();
    }

    public async Task<decimal> SumActiveValueByAccountAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        Direction? direction = null)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Date == date &&
                transaction.Status == TransactionStatus.Active);

        if (direction is { } selectedDirection)
        {
            query = query.Where(transaction => transaction.Direction == selectedDirection);
        }

        return await query.Select(transaction => (decimal?)transaction.Value).SumAsync() ?? 0m;
    }

    private static IQueryable<Transaction> ApplyFilter(
        IQueryable<Transaction> source,
        Guid branchId,
        TransactionListFilter filter)
    {
        var query = source.Where(transaction => transaction.BranchId == branchId);

        if (filter.AllowedAccountIds is not null)
        {
            query = query.Where(transaction => filter.AllowedAccountIds.Contains(transaction.AccountId));
        }

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(transaction => transaction.AccountId == accountId);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            query = query.Where(transaction => transaction.Date >= dateFrom);
        }

        if (filter.DateTo is { } dateTo)
        {
            query = query.Where(transaction => transaction.Date <= dateTo);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(transaction => transaction.Status == status);
        }

        if (filter.OperatorId is { } operatorId)
        {
            query = query.Where(transaction => transaction.RecordedByOperatorId == operatorId);
        }

        if (filter.ClientId is { } clientId)
        {
            query = query.Where(transaction => transaction.ClientId == clientId);
        }

        return query;
    }
}
