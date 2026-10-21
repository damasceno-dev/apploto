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

    /// <summary>
    /// The same official (non-Draft) variance rows folded per close status in the database (M11 Phase 7 review) —
    /// totals without materializing the rows.
    /// </summary>
    Task<IReadOnlyList<OfficialVarianceStatusTotalRow>> ListOfficialVarianceStatusTotalsAsNoTracking(
        Guid branchId, Guid productId, Guid? accountId, DateTime dateFrom, DateTime dateTo,
        CancellationToken ct = default);

    /// <summary>
    /// The first <paramref name="take"/> official variance rows ordered by absolute value DESC, <c>Date DESC</c>, close
    /// id — the preview limited in the database.
    /// </summary>
    Task<IReadOnlyList<VarianceTimeSeriesRow>> ListLargestOfficialVariancesAsNoTracking(
        Guid branchId, Guid productId, DateTime dateFrom, DateTime dateTo, int take, CancellationToken ct = default);
}
