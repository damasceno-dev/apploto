using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

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
}
