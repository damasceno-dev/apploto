using server.Communication.Requests;
using server.Domain.Entities;
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
            .ListActiveByDailyCloseIdAsNoTracking(currentDailyCloseId, ct);
        var totalClosing = currentItems
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);

        return await CalculateFromTotalClosingAsync(
            branchId,
            accountId,
            branchLocalDate,
            totalClosing,
            cashVarianceProductId,
            ct);
    }

    public async Task<decimal> CalculateWithOpeningSourceAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        Guid currentDailyCloseId,
        Guid cashVarianceProductId,
        DailyClose? openingSource,
        CancellationToken ct)
    {
        var currentItems = await dailyCloseItemsRepository
            .ListActiveByDailyCloseIdAsNoTracking(currentDailyCloseId, ct);
        var totalClosing = currentItems
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);

        return await CalculateFromResolvedOpeningAsync(
            branchId,
            accountId,
            branchLocalDate,
            totalClosing,
            cashVarianceProductId,
            openingSource,
            ct);
    }

    public Task<decimal> CalculateCandidateAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> candidateValues,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        var totalClosing = candidateValues
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);

        return CalculateFromTotalClosingAsync(
            branchId,
            accountId,
            branchLocalDate,
            totalClosing,
            cashVarianceProductId,
            ct);
    }

    private async Task<decimal> CalculateFromTotalClosingAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        decimal totalClosing,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var priorClose = await dailyClosesRepository
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                branchId,
                accountId,
                branchLocalDate,
                ct);

        return await CalculateFromResolvedOpeningAsync(
            branchId,
            accountId,
            branchLocalDate,
            totalClosing,
            cashVarianceProductId,
            priorClose,
            ct);
    }

    private async Task<decimal> CalculateFromResolvedOpeningAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        decimal totalClosing,
        Guid cashVarianceProductId,
        DailyClose? openingSource,
        CancellationToken ct)
    {
        var totalOpening = openingSource?.Items
            .Where(item => item.Active && item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value) ?? 0m;

        ct.ThrowIfCancellationRequested();

        var totalTransactionsIn = await transactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                branchId,
                accountId,
                branchLocalDate,
                Direction.In,
                ct);
        var totalTransactionsOut = await transactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                branchId,
                accountId,
                branchLocalDate,
                Direction.Out,
                ct);

        return totalClosing - totalOpening - (totalTransactionsIn - totalTransactionsOut);
    }
}
