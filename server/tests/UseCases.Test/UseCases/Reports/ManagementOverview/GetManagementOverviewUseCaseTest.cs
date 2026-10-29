using CommonTestUtilities.Entities;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.ManagementOverview;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.ManagementOverview;

/// <summary>
/// The overview's failure model before any section reads: cancellation, role, validation and a month that has not
/// started all fail the whole request without opening the snapshot or reading the clock twice. Section assembly and
/// parity are pinned against real PostgreSQL in WebApi.Test.
/// </summary>
public class GetManagementOverviewUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2026, 10, 20, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BranchLocalNow = new(2026, 10, 20, 10, 0, 0);

    [Fact]
    public async Task Execute_ShouldThrow403_BeforeReadingTheClockOrOpeningTheSnapshot_WhenCallerIsMember()
    {
        var ctx = Context(new BranchUserBuilder().WithRole(Role.Member).Build());

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => ctx.UseCase.Execute(new RequestManagementOverviewJson()));

        ctx.Clock.DidNotReceive().UtcNow();
        await ctx.Snapshot.DidNotReceiveWithAnyArgs().Begin(default);
    }

    [Fact]
    public async Task Execute_ShouldFailValidation_BeforeOpeningTheSnapshot()
    {
        var ctx = Context(new BranchUserBuilder().WithRole(Role.Manager).Build());

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            ctx.UseCase.Execute(new RequestManagementOverviewJson { Year = 2026 }));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.REPORT_YEAR_MONTH_PAIR_REQUIRED]);
        await ctx.Snapshot.DidNotReceiveWithAnyArgs().Begin(default);
    }

    [Fact]
    public async Task Execute_ShouldRefuseAMonthThatHasNotStarted_WithOneClockRead_AndNoSnapshot()
    {
        var ctx = Context(new BranchUserBuilder().WithRole(Role.Admin).Build());

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            ctx.UseCase.Execute(new RequestManagementOverviewJson { Year = 2026, Month = 11 }));

        exception.GetErrorMessages.ShouldBe([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        ctx.Clock.Received(1).UtcNow();
        await ctx.Snapshot.DidNotReceiveWithAnyArgs().Begin(default);
    }

    [Fact]
    public async Task Execute_ShouldNotStart_WhenTheRequestIsAlreadyCancelled()
    {
        var ctx = Context(new BranchUserBuilder().WithRole(Role.Manager).Build());
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            ctx.UseCase.Execute(new RequestManagementOverviewJson(), cancelled.Token));

        await ctx.Snapshot.DidNotReceiveWithAnyArgs().Begin(default);
    }

    private sealed record TestContext(GetManagementOverviewUseCase UseCase, IBranchClock Clock, IReportReadSnapshot Snapshot);

    private static TestContext Context(BranchUser branchUser)
    {
        var clock = Substitute.For<IBranchClock>();
        clock.UtcNow().Returns(FixedUtcNow);
        clock.LocalBusinessDateTime(FixedUtcNow).Returns(BranchLocalNow);
        var snapshot = Substitute.For<IReportReadSnapshot>();
        var transactions = Substitute.For<ITransactionsRepository>();
        var types = Substitute.For<ITransactionTypesRepository>();
        var closes = Substitute.For<IDailyClosesRepository>();
        var items = Substitute.For<IDailyCloseItemsRepository>();
        var accounts = Substitute.For<IAccountsRepository>();
        var operators = Substitute.For<IOperatorsRepository>();
        var resolver = Substitute.For<ICashVarianceProductResolver>();
        var projector = new TimeEntryBalanceProjector(operators, Substitute.For<ITimeEntriesRepository>(),
            Substitute.For<ITimeEntryPoliciesRepository>(), new TimeEntryCalculationService());
        var lockDateReader = Substitute.For<ILockDateReader>();
        var builder = new ManagementOverviewBuilder(
            new ManagerQueueReader(closes, items, accounts, transactions, resolver, lockDateReader, Substitute.For<IOnboardingRepository>(),
                Substitute.For<ISessionHandoversRepository>(),
                Substitute.For<ISessionOpeningDiscrepanciesRepository>(),
                Substitute.For<ICustodyMappingIssuesRepository>()),
            new MonthlyReconciliationReader(closes, transactions, items, resolver, new MonthLockReadinessEvaluator(), lockDateReader),
            new UpcomingSettlementsReader(transactions, types),
            new MonthlyFinancialReader(closes, items, transactions, types, resolver, projector),
            projector, closes, items, transactions, resolver);
        var useCase = new GetManagementOverviewUseCase(
            new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build(), clock, snapshot, builder);
        return new TestContext(useCase, clock, snapshot);
    }
}
