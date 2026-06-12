using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class DailyCloseItemsRepository(ServerDbContext dbContext) : IDailyCloseItemsRepository
{
    public async Task Add(DailyCloseItem dailyCloseItem)
    {
        await dbContext.DailyCloseItems.AddAsync(dailyCloseItem);
    }

    public async Task AddRange(IEnumerable<DailyCloseItem> dailyCloseItems)
    {
        await dbContext.DailyCloseItems.AddRangeAsync(dailyCloseItems);
    }

    public async Task<IReadOnlyList<DailyCloseItem>> ListActiveByDailyCloseIdAsNoTracking(Guid dailyCloseId)
    {
        return await dbContext.DailyCloseItems
            .AsNoTracking()
            .Where(item =>
                item.DailyCloseId == dailyCloseId &&
                item.Active)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VarianceTimeSeriesRow>> ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo)
    {
        return await dbContext.DailyCloseItems
            .AsNoTracking()
            .Where(item =>
                item.Active &&
                item.ProductId == productId &&
                item.DailyClose.BranchId == branchId &&
                item.DailyClose.Active &&
                item.DailyClose.Date >= dateFrom &&
                item.DailyClose.Date <= dateTo &&
                (accountId == null || item.DailyClose.AccountId == accountId))
            .OrderBy(item => item.DailyClose.Date)
            .ThenBy(item => item.DailyClose.Account.Name)
            .Select(item => new VarianceTimeSeriesRow(
                item.DailyClose.Date,
                item.DailyClose.AccountId,
                item.DailyClose.Account.Name,
                item.Value,
                item.DailyClose.Status))
            .ToListAsync();
    }
}
