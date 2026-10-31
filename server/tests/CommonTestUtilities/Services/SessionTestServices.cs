using server.Domain.Entities;
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

    /// <summary>
    /// A count service whose latest attempt is the session's current confirmed one: same token and revisions, with
    /// <paramref name="difference"/> as its calculation (the submit gate's happy path).
    /// </summary>
    public static SessionCountService CurrentAttemptCountService(
        Func<DailyClose> session,
        decimal difference,
        ICashVarianceProductResolver cashVarianceProductResolver,
        IDailyClosesRepository dailyClosesRepository)
    {
        var attempts = Substitute.For<ISessionCountAttemptsRepository>();
        attempts.GetBySessionIdAndNumberAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => CurrentAttempt(session(), difference));
        return new SessionCountService(
            OpeningResolver(dailyClosesRepository, cashVarianceProductResolver),
            Substitute.For<ITransactionsRepository>(),
            Substitute.For<IDailyCloseItemsRepository>(),
            attempts,
            cashVarianceProductResolver);
    }

    /// <summary>The deterministic current attempt of <paramref name="session"/> (id derived from its token).</summary>
    public static SessionCountAttempt CurrentAttempt(DailyClose session, decimal difference)
    {
        return new SessionCountAttempt
        {
            Id = AttemptIdFor(session),
            BranchId = session.BranchId,
            SessionId = session.Id,
            AttemptNumber = Math.Max(1, session.LastAttemptNumber),
            ConfirmationToken = session.CurrentConfirmationToken,
            CountRevision = session.CountRevision,
            LedgerRevision = session.LedgerRevision,
            OpeningRevision = session.OpeningRevision,
            TotalClosing = difference,
            Difference = difference
        };
    }

    public static Guid AttemptIdFor(DailyClose session)
    {
        var bytes = session.CurrentConfirmationToken.ToByteArray();
        bytes[0] ^= 0x5A;
        return new Guid(bytes);
    }

    /// <summary>A revision service whose session roots are throwaway sessions (unit tests assert the rows, not the bump).</summary>
    public static SessionRevisionService Revisions(IDailyClosesRepository? dailyClosesRepository = null)
    {
        var repository = dailyClosesRepository ?? Substitute.For<IDailyClosesRepository>();
        repository.GetRootForRevision(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => new DailyClose { Id = call.ArgAt<Guid>(0), BranchId = call.ArgAt<Guid>(1) });
        return new SessionRevisionService(repository);
    }

    /// <summary>
    /// The read projector over the given session repository: no attempt exists, so the candidate is NotConferred and
    /// the allowed actions come from the real guards.
    /// </summary>
    public static server.Application.UseCases.Sessions.SessionCountProjector CountProjector(
        IDailyClosesRepository dailyClosesRepository,
        ICashVarianceProductResolver cashVarianceProductResolver,
        IDailyCloseWorkflowGuard? workflowGuard = null,
        server.Application.Services.Settings.ILockDateReader? lockDateReader = null)
    {
        var countService = new SessionCountService(
            OpeningResolver(dailyClosesRepository, cashVarianceProductResolver),
            Substitute.For<ITransactionsRepository>(),
            Substitute.For<IDailyCloseItemsRepository>(),
            Substitute.For<ISessionCountAttemptsRepository>(),
            cashVarianceProductResolver);
        var reader = lockDateReader ?? Substitute.For<server.Application.Services.Settings.ILockDateReader>();
        var actions = new SessionAllowedActionsResolver(
            workflowGuard ?? Substitute.For<IDailyCloseWorkflowGuard>(),
            reader,
            Substitute.For<IBranchUsersRepository>());
        return new server.Application.UseCases.Sessions.SessionCountProjector(
            dailyClosesRepository,
            cashVarianceProductResolver,
            countService,
            actions);
    }
}
