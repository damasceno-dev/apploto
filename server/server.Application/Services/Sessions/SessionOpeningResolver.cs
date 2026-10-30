using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

public sealed class SessionOpeningResolver(
    IDailyClosesRepository dailyClosesRepository,
    IOpeningCountsRepository openingCountsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResolvedSessionOpening> ResolveForCalculation(DailyClose session, CancellationToken ct = default)
    {
        if (session.Opening is { } acknowledged)
        {
            return new ResolvedSessionOpening(
                acknowledged.Items.ToDictionary(item => item.ProductId, item => item.AcknowledgedValue),
                acknowledged,
                null);
        }

        var predecessor = await dailyClosesRepository.GetCountedPredecessorAsNoTracking(session, ct);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(session.BranchId, ct);
        return new ResolvedSessionOpening(ClosingValues(predecessor, cashVarianceProductId), null, predecessor);
    }

    /// <summary>
    /// The proposal for the cashbox's next session: the latest counted Ready session's closing, else the current
    /// opening count, else missing. Read under the cashbox coordination when a start depends on it.
    /// </summary>
    public async Task<SessionOpeningProposal> ProposeForNextSession(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
        var latest = await dailyClosesRepository.GetLatestCountedReadyByAccountIdAsNoTracking(branchId, accountId, ct);
        if (latest is not null)
        {
            return new SessionOpeningProposal(
                SessionOpeningSourceKind.PreviousSession,
                latest.Id,
                latest.CountRevision,
                latest.Date,
                ClosingValues(latest, cashVarianceProductId));
        }

        var openingCount = await openingCountsRepository.GetCurrentByAccountIdAsNoTracking(branchId, accountId, ct);
        if (openingCount is not null)
        {
            return new SessionOpeningProposal(
                SessionOpeningSourceKind.OpeningCount,
                openingCount.Id,
                openingCount.Revision,
                openingCount.AsOfDate,
                openingCount.Items.ToDictionary(item => item.ProductId, item => item.Value));
        }

        return SessionOpeningProposal.Missing;
    }

    public static IReadOnlyDictionary<Guid, decimal> ClosingValues(DailyClose? session, Guid cashVarianceProductId)
    {
        return session?.Items
            .Where(item => item.Active && item.ProductId != cashVarianceProductId)
            .ToDictionary(item => item.ProductId, item => item.Value)
            ?? new Dictionary<Guid, decimal>();
    }
}
