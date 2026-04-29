using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IDailyCloseItemsRepository
{
    Task Add(DailyCloseItem dailyCloseItem);
    Task AddRange(IEnumerable<DailyCloseItem> dailyCloseItems);
    Task<IReadOnlyList<DailyCloseItem>> ListActiveByDailyCloseIdAsNoTracking(Guid dailyCloseId);
}
