using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class DailyCloseItemsRepository(ServerDbContext dbContext) : IDailyCloseItemsRepository
{
    public async Task Add(DailyCloseItem dailyCloseItem, CancellationToken ct = default)
    {
        await dbContext.DailyCloseItems.AddAsync(dailyCloseItem, ct);
    }

    public async Task<IReadOnlyList<DailyCloseItem>> ListActiveByDailyCloseIdAsNoTracking(
        Guid dailyCloseId,
        CancellationToken ct = default)
    {
        return await dbContext.DailyCloseItems
            .AsNoTracking()
            .Where(item =>
                item.DailyCloseId == dailyCloseId &&
                item.Active)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<VarianceTimeSeriesRow>> ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo,
        CancellationToken ct = default)
    {
        return await BuildOfficialVarianceQuery(branchId, productId, accountId, dateFrom, dateTo)
            .OrderBy(item => item.DailyClose.Date)
            .ThenBy(item => item.DailyClose.Account.Name)
            .Select(item => new VarianceTimeSeriesRow(
                item.DailyClose.Date,
                item.DailyClose.AccountId,
                item.DailyClose.Account.Name,
                item.Value,
                item.DailyClose.Status,
                item.DailyCloseId))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OfficialVarianceStatusTotalRow>> ListOfficialVarianceStatusTotalsAsNoTracking(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo,
        CancellationToken ct = default)
    {
        var rows = await BuildOfficialVarianceQuery(branchId, productId, accountId, dateFrom, dateTo)
            .GroupBy(item => item.DailyClose.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                Total = group.Sum(item => item.Value),
                Max = group.Max(item => item.Value),
                Min = group.Min(item => item.Value)
            })
            .ToListAsync(ct);

        return rows.Select(row => new OfficialVarianceStatusTotalRow(row.Status, row.Count, row.Total, row.Max, row.Min)).ToList();
    }

    public async Task<IReadOnlyList<VarianceTimeSeriesRow>> ListLargestOfficialVariancesAsNoTracking(
        Guid branchId, Guid productId, DateTime dateFrom, DateTime dateTo, int take, CancellationToken ct = default)
    {
        return await BuildOfficialVarianceQuery(branchId, productId, accountId: null, dateFrom, dateTo)
            .OrderByDescending(item => Math.Abs(item.Value))
            .ThenByDescending(item => item.DailyClose.Date)
            .ThenBy(item => item.DailyCloseId)
            .Take(take)
            .Select(item => new VarianceTimeSeriesRow(
                item.DailyClose.Date,
                item.DailyClose.AccountId,
                item.DailyClose.Account.Name,
                item.Value,
                item.DailyClose.Status,
                item.DailyCloseId))
            .ToListAsync(ct);
    }

    // The official variance predicate: active items of the variance product on active non-Draft closes, so a
    // recalled or recheck-demoted (Draft) close's retained value is never read. Every variance read composes it.
    private IQueryable<DailyCloseItem> BuildOfficialVarianceQuery(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo)
    {
        return dbContext.DailyCloseItems
            .AsNoTracking()
            .Where(item =>
                item.Active &&
                item.ProductId == productId &&
                item.DailyClose.BranchId == branchId &&
                item.DailyClose.Active &&
                item.DailyClose.Status != DailyCloseStatus.Draft &&
                item.DailyClose.Date >= dateFrom &&
                item.DailyClose.Date <= dateTo &&
                (accountId == null || item.DailyClose.AccountId == accountId));
    }
}
