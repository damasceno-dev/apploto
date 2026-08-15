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
        return await dbContext.DailyCloseItems
            .AsNoTracking()
            .Where(item =>
                item.Active &&
                item.ProductId == productId &&
                item.DailyClose.BranchId == branchId &&
                item.DailyClose.Active &&
                item.DailyClose.Status != DailyCloseStatus.Draft &&
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
            .ToListAsync(ct);
    }
}
