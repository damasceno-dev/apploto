using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

public class CashVarianceCalculator(
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository) : ICashVarianceCalculator
{
    public async Task<decimal> CalculateAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        Guid currentDailyCloseId,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var currentItems = await dailyCloseItemsRepository
            .ListActiveByDailyCloseIdAsNoTracking(currentDailyCloseId);
        var totalClosing = currentItems
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);

        ct.ThrowIfCancellationRequested();

        var priorClose = await dailyClosesRepository
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                branchId,
                accountId,
                branchLocalDate);
        var totalOpening = priorClose?.Items
            .Where(item => item.Active && item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value) ?? 0m;

        ct.ThrowIfCancellationRequested();

        var totalTransactionsIn = await transactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                branchId,
                accountId,
                branchLocalDate,
                Direction.In);
        var totalTransactionsOut = await transactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                branchId,
                accountId,
                branchLocalDate,
                Direction.Out);

        return totalClosing - totalOpening - (totalTransactionsIn - totalTransactionsOut);
    }
}
