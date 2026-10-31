using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

/// <summary>One §6.12 calculation of a session from its persisted count, opening and Active rows.</summary>
public sealed record SessionCountCalculation(
    decimal TotalOpening,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalClosing,
    IReadOnlyList<SessionCountCalculationItem> Items)
{
    public decimal ExpectedClosing => TotalOpening + TotalIn - TotalOut;

    /// <summary>CashVariance = TotalClosing − TotalOpening − (In − Out), exactly the shipped formula.</summary>
    public decimal Difference => TotalClosing - ExpectedClosing;
}

public sealed record SessionCountCalculationItem(Guid ProductId, decimal? OpeningValue, decimal? CountedValue);

/// <summary>
/// Blind count (§6.20, M9.6 Phase 4): computes the authoritative calculation only for Conferir, a submit check or the
/// labelled unconfirmed recalculation, and decides the server-owned candidate state. A read never allocates a token or
/// writes anything; a Draft calculation reaches a caller only through an attempt the caller confirmed.
/// </summary>
public sealed class SessionCountService(
    SessionOpeningResolver openingResolver,
    ITransactionsRepository transactionsRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    ISessionCountAttemptsRepository attemptsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<SessionCountCalculation> Calculate(DailyClose session, CancellationToken ct = default)
    {
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(session.BranchId, ct);
        var opening = await openingResolver.ResolveForCalculation(session, ct);
        var counted = (await dailyCloseItemsRepository.ListActiveByDailyCloseIdAsNoTracking(session.Id, ct))
            .Where(item => item.ProductId != cashVarianceProductId)
            .ToDictionary(item => item.ProductId, item => item.Value);
        var openingValues = opening.Values
            .Where(pair => pair.Key != cashVarianceProductId)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        var totalIn = await transactionsRepository.SumActiveValueBySessionAsNoTracking(session, Direction.In, ct);
        var totalOut = await transactionsRepository.SumActiveValueBySessionAsNoTracking(session, Direction.Out, ct);

        var items = openingValues.Keys
            .Union(counted.Keys)
            .Order()
            .Select(productId => new SessionCountCalculationItem(
                productId,
                openingValues.TryGetValue(productId, out var openingValue) ? openingValue : null,
                counted.TryGetValue(productId, out var countedValue) ? countedValue : null))
            .ToList();

        return new SessionCountCalculation(
            openingValues.Values.Sum(),
            totalIn,
            totalOut,
            counted.Values.Sum(),
            items);
    }

    public Task<SessionCountAttempt?> GetLatestAttempt(DailyClose session, CancellationToken ct = default)
    {
        return session.LastAttemptNumber == 0
            ? Task.FromResult<SessionCountAttempt?>(null)
            : attemptsRepository.GetBySessionIdAndNumberAsNoTracking(session.BranchId, session.Id, session.LastAttemptNumber, ct);
    }

    public static SessionCandidateState StateOf(DailyClose session, SessionCountAttempt? latest)
    {
        if (latest is null)
            return SessionCandidateState.NotConferred;
        if (latest.CountRevision != session.CountRevision)
            return SessionCandidateState.Outdated;
        if (session.RecountStartedForAttemptId == latest.Id)
            return SessionCandidateState.Recounting;
        if (latest.LedgerRevision != session.LedgerRevision || latest.OpeningRevision != session.OpeningRevision)
            return SessionCandidateState.Unconfirmed;

        return latest.ConfirmationToken == session.CurrentConfirmationToken
            ? SessionCandidateState.Current
            : SessionCandidateState.Outdated;
    }

    /// <summary>The unchanged-count/ledger-changed path: the next Conferir needs <c>ConfirmCountStillObserved</c>.</summary>
    public static bool RequiresStillObserved(DailyClose session, SessionCountAttempt? latest)
    {
        return StateOf(session, latest) == SessionCandidateState.Unconfirmed;
    }

    public static SessionCountAttemptKind KindOfNext(DailyClose session, SessionCountAttempt? latest)
    {
        if (latest is null)
            return SessionCountAttemptKind.Initial;
        if (session.RecountStartedForAttemptId == latest.Id)
            return SessionCountAttemptKind.Recount;
        return latest.CountRevision != session.CountRevision
            ? SessionCountAttemptKind.Correction
            : SessionCountAttemptKind.StillObserved;
    }

    /// <summary>Builds the read projection; only an Unconfirmed state computes (never persists) a recalculation.</summary>
    public async Task<ResponseSessionCountJson> BuildResponse(DailyClose session, CancellationToken ct = default)
    {
        var latest = await GetLatestAttempt(session, ct);
        var state = StateOf(session, latest);

        ResponseSessionUnconfirmedRecalculationJson? recalculation = null;
        if (state == SessionCandidateState.Unconfirmed)
        {
            var current = await Calculate(session, ct);
            recalculation = new ResponseSessionUnconfirmedRecalculationJson
            {
                CountRevision = session.CountRevision,
                LedgerRevision = session.LedgerRevision,
                OpeningRevision = session.OpeningRevision,
                TotalOpening = current.TotalOpening,
                TotalIn = current.TotalIn,
                TotalOut = current.TotalOut,
                ExpectedClosing = current.ExpectedClosing,
                TotalClosing = current.TotalClosing,
                Difference = current.Difference
            };
        }

        return new ResponseSessionCountJson
        {
            ConfirmationToken = session.CurrentConfirmationToken,
            CountRevision = session.CountRevision,
            LedgerRevision = session.LedgerRevision,
            OpeningRevision = session.OpeningRevision,
            CandidateState = state,
            RequiresStillObservedConfirmation = state == SessionCandidateState.Unconfirmed,
            LatestAttemptId = latest?.Id,
            LatestAttemptNumber = latest?.AttemptNumber,
            SubmittedAttemptId = session.SubmittedAttemptId,
            ConfirmedAttempt = state is SessionCandidateState.Current or SessionCandidateState.Unconfirmed
                ? latest!.ToResponse()
                : null,
            UnconfirmedRecalculation = recalculation
        };
    }
}

public static class SessionCountAttemptMapper
{
    extension(SessionCountAttempt attempt)
    {
        public ResponseSessionCountAttemptJson ToResponse()
        {
            return new ResponseSessionCountAttemptJson
            {
                Id = attempt.Id,
                SessionId = attempt.SessionId,
                AttemptNumber = attempt.AttemptNumber,
                Kind = attempt.Kind,
                CountRevision = attempt.CountRevision,
                LedgerRevision = attempt.LedgerRevision,
                OpeningRevision = attempt.OpeningRevision,
                TotalOpening = attempt.TotalOpening,
                TotalIn = attempt.TotalIn,
                TotalOut = attempt.TotalOut,
                ExpectedClosing = attempt.ExpectedClosing,
                TotalClosing = attempt.TotalClosing,
                Difference = attempt.Difference,
                ConferredByUserId = attempt.ConferredByUserId,
                ConferredByName = attempt.ConferredByName,
                ConferredByOperatorId = attempt.ConferredByOperatorId,
                ConferredAt = attempt.CreatedAt,
                Items = attempt.Items
                    .OrderBy(item => item.Product?.DisplayOrder ?? int.MaxValue)
                    .ThenBy(item => item.ProductId)
                    .Select(item => new ResponseSessionCountAttemptItemJson
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product?.Name ?? string.Empty,
                        OpeningValue = item.OpeningValue,
                        CountedValue = item.CountedValue
                    })
                    .ToList()
            };
        }
    }
}
