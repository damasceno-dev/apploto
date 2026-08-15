using server.Domain.Entities;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IDailyCloseItemsRepository
{
    Task Add(DailyCloseItem dailyCloseItem, CancellationToken ct = default);
    Task<IReadOnlyList<DailyCloseItem>> ListActiveByDailyCloseIdAsNoTracking(Guid dailyCloseId, CancellationToken ct = default);
    Task<IReadOnlyList<VarianceTimeSeriesRow>> ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo,
        CancellationToken ct = default);
}
