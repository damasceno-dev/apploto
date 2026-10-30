using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Sessions;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

/// <summary>Session services wired to substitutes for use case tests that do not exercise custody.</summary>
public static class SessionTestServices
{
    public static SessionHandoverInvalidator HandoverInvalidator(ISessionHandoversRepository? handovers = null)
    {
        return new SessionHandoverInvalidator(
            handovers ?? Substitute.For<ISessionHandoversRepository>(),
            new SessionEventRecorder(Substitute.For<ISessionEventsRepository>()));
    }

    public static OpeningDiscrepancyRecorder DiscrepancyRecorder(ISessionOpeningDiscrepanciesRepository? discrepancies = null)
    {
        return new OpeningDiscrepancyRecorder(
            discrepancies ?? Substitute.For<ISessionOpeningDiscrepanciesRepository>(),
            new SessionEventRecorder(Substitute.For<ISessionEventsRepository>()));
    }

    public static SessionOpeningResolver OpeningResolver(
        IDailyClosesRepository dailyClosesRepository,
        ICashVarianceProductResolver cashVarianceProductResolver,
        IOpeningCountsRepository? openingCounts = null)
    {
        return new SessionOpeningResolver(
            dailyClosesRepository,
            openingCounts ?? Substitute.For<IOpeningCountsRepository>(),
            cashVarianceProductResolver);
    }

    /// <summary>A ledger guard substitute whose new rows bind no session (non-cashbox default).</summary>
    public static IDailyCloseLedgerGuard LedgerGuard()
    {
        var guard = Substitute.For<IDailyCloseLedgerGuard>();
        guard.ResolveSessionForNewRow(
                Arg.Any<Guid>(),
                Arg.Any<server.Domain.Entities.Enums.AccountType>(),
                Arg.Any<DateTime>(),
                Arg.Any<SessionRowContext>(),
                Arg.Any<CancellationToken>())
            .Returns(SessionRowResolution.None);
        return guard;
    }
}
