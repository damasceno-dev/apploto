using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

public class CashVarianceCalculator(
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    SessionOpeningResolver openingResolver,
    ITransactionsRepository transactionsRepository) : ICashVarianceCalculator
{
    public async Task<decimal> CalculateAsync(DailyClose session, Guid cashVarianceProductId, CancellationToken ct)
    {
        var opening = await openingResolver.ResolveForCalculation(session, ct);
        return await CalculateWithOpeningAsync(session, opening, cashVarianceProductId, ct);
    }

    public async Task<decimal> CalculateCandidateAsync(
        DailyClose session,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> candidateValues,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        var totalClosing = candidateValues
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);
        var opening = await openingResolver.ResolveForCalculation(session, ct);
        return await CalculateFromTotals(session, totalClosing, opening.Total(cashVarianceProductId), ct);
    }

    public async Task<decimal> CalculateWithOpeningAsync(
        DailyClose session,
        ResolvedSessionOpening opening,
        Guid cashVarianceProductId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var currentItems = await dailyCloseItemsRepository.ListActiveByDailyCloseIdAsNoTracking(session.Id, ct);
        var totalClosing = currentItems
            .Where(item => item.ProductId != cashVarianceProductId)
            .Sum(item => item.Value);

        return await CalculateFromTotals(session, totalClosing, opening.Total(cashVarianceProductId), ct);
    }

    private async Task<decimal> CalculateFromTotals(
        DailyClose session,
        decimal totalClosing,
        decimal totalOpening,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalTransactionsIn = await transactionsRepository.SumActiveValueBySessionAsNoTracking(session, Direction.In, ct);
        var totalTransactionsOut = await transactionsRepository.SumActiveValueBySessionAsNoTracking(session, Direction.Out, ct);

        return totalClosing - totalOpening - (totalTransactionsIn - totalTransactionsOut);
    }
}
