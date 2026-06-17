using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Repositories;

internal class DailyClosesRepository(ServerDbContext dbContext) : IDailyClosesRepository
{
    public async Task Add(DailyClose dailyClose)
    {
        await dbContext.DailyCloses.AddAsync(dailyClose);
    }

    public async Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId)
    {
        return await IncludeRichResponse(dbContext.DailyCloses)
            .FirstOrDefaultAsync(dailyClose =>
                dailyClose.Id == id &&
                dailyClose.BranchId == branchId &&
                dailyClose.Active);
    }

    public async Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await IncludeRichResponse(dbContext.DailyCloses)
            .AsNoTracking()
            .FirstOrDefaultAsync(dailyClose =>
                dailyClose.Id == id &&
                dailyClose.BranchId == branchId &&
                dailyClose.Active);
    }

    public async Task<DailyClose?> GetByBranchIdAndAccountIdAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        CancellationToken ct = default)
    {
        // Exact-day open-close lookup for the edit-impact preview's cash-variance section.
        // Status only — no Items include needed. Ordered for determinism in the unlikely
        // event of more than one active close on the same (branch, account, date).
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.Date == date &&
                dailyClose.Active)
            .OrderByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DailyClose?> GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime beforeDate)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Items)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.Date < beforeDate &&
                dailyClose.Active)
            .OrderByDescending(dailyClose => dailyClose.Date)
            .ThenByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<DailyClose>> ListByBranchIdAndYearMonthAsNoTracking(Guid branchId, int year, int month)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Account)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.Date.Year == year &&
                dailyClose.Date.Month == month)
            .OrderBy(dailyClose => dailyClose.Date)
            .ThenBy(dailyClose => dailyClose.Account.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(
        Guid branchId,
        DailyCloseListFilter filter)
    {
        return await ApplyFilter(dbContext.DailyCloses, branchId, filter)
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Account)
            .Include(dailyClose => dailyClose.SubmittedByOperator)
            .Include(dailyClose => dailyClose.ApprovedByUser)
            .OrderByDescending(dailyClose => dailyClose.Date)
            .ThenByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAsNoTracking(Guid branchId, DailyCloseListFilter filter)
    {
        return await ApplyFilter(dbContext.DailyCloses, branchId, filter)
            .AsNoTracking()
            .CountAsync();
    }

    private static IQueryable<DailyClose> IncludeRichResponse(IQueryable<DailyClose> source)
    {
        return source
            .Include(dailyClose => dailyClose.Account)
            .Include(dailyClose => dailyClose.SubmittedByOperator)
            .Include(dailyClose => dailyClose.ApprovedByUser)
            .Include(dailyClose => dailyClose.Items)
            .ThenInclude(item => item.Product);
    }

    private static IQueryable<DailyClose> ApplyFilter(IQueryable<DailyClose> source,Guid branchId,DailyCloseListFilter filter)
    {
        var query = source.Where(dailyClose =>
            dailyClose.BranchId == branchId &&
            dailyClose.Active);

        if (filter.AllowedAccountIds is not null)
            query = query.Where(dailyClose => filter.AllowedAccountIds.Contains(dailyClose.AccountId));

        if (filter.AccountId is { } accountId)
            query = query.Where(dailyClose => dailyClose.AccountId == accountId);

        if (filter.Status is { } status)
            query = query.Where(dailyClose => dailyClose.Status == status);

        if (filter.DateFrom is { } dateFrom)
            query = query.Where(dailyClose => dailyClose.Date >= dateFrom);

        if (filter.DateTo is { } dateTo)
            query = query.Where(dailyClose => dailyClose.Date <= dateTo);

        if (filter.OperatorId is { } operatorId)
            query = query.Where(dailyClose => dailyClose.SubmittedByOperatorId == operatorId);

        return query;
    }
}
