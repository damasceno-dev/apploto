using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.Reports.MonthlyReconciliation;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.MonthlyReconciliation;

public class GetMonthlyReconciliationUseCaseTest
{
    private const int Year = 2025;
    private const int Month = 5;
    private static readonly DateTime DateFrom = new(Year, Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime DateTo = new(Year, Month, 31, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public async Task Execute_ShouldReturnLockReadyTrue_WhenAllClosesApprovedAndNoDraftTransactions()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).WithName("Terminal A").Build();
        var close = BuildClose(branchUser.BranchId, account, DateFrom, DailyCloseStatus.Approved);
        var rows = new List<VarianceTimeSeriesRow>
        {
            new(DateFrom, account.Id, account.Name, 15m, DailyCloseStatus.Approved)
        };
        var counts = new List<MonthlyTransactionCountRow>
        {
            new(DateFrom, TransactionStatus.Active, 2)
        };

        var ctx = BuildContext(branchUser, closes: [close], varianceRows: rows, statusCounts: counts);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.LockReady.ShouldBeTrue();
        response.Blockers.ShouldBeEmpty();
        response.Days.Count.ShouldBe(31);
        response.Days[0].ActiveTransactionCount.ShouldBe(2);
        response.Days[0].DraftTransactionCount.ShouldBe(0);
        response.Days[0].NetVariance.ShouldBe(15m);
        response.Days[0].Closes[0].VarianceValue.ShouldBe(15m);
    }

    [Fact]
    public async Task Execute_ShouldReturnBlocker_WhenCloseIsDraft()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).WithName("Terminal Draft").Build();
        var close = BuildClose(branchUser.BranchId, account, DateFrom.AddDays(3), DailyCloseStatus.Draft);

        var ctx = BuildContext(branchUser, closes: [close], varianceRows: [], statusCounts: []);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.LockReady.ShouldBeFalse();
        var blocker = response.Blockers.ShouldHaveSingleItem();
        blocker.Type.ShouldBe(MonthlyReconciliationBlockerType.UnapprovedClose);
        blocker.Day.ShouldBe(4);
        blocker.DailyCloseId.ShouldBe(close.Id);
        blocker.AccountId.ShouldBe(account.Id);
        blocker.AccountName.ShouldBe("Terminal Draft");
        blocker.CloseStatus.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task Execute_ShouldReturnBlocker_WhenDraftTransactionsExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var counts = new List<MonthlyTransactionCountRow>
        {
            new(DateFrom.AddDays(9), TransactionStatus.Draft, 3)
        };

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], statusCounts: counts);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.LockReady.ShouldBeFalse();
        var blocker = response.Blockers.ShouldHaveSingleItem();
        blocker.Type.ShouldBe(MonthlyReconciliationBlockerType.DraftTransactions);
        blocker.Day.ShouldBe(10);
        blocker.DraftTransactionCount.ShouldBe(3);
        response.Days[9].DraftTransactionCount.ShouldBe(3);
    }

    [Fact]
    public async Task Execute_ShouldReturnMissingExpectedCloseBlocker_ForDirectTerminalActivity()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var activity = new TerminalActivityPairRow(
            DateFrom.AddDays(5), accountId, "Terminal sem fechamento");

        var ctx = BuildContext(
            branchUser,
            closes: [],
            varianceRows: [],
            statusCounts: [],
            activityPairs: [activity]);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.LockReady.ShouldBeFalse();
        var blocker = response.Blockers.ShouldHaveSingleItem();
        blocker.Type.ShouldBe(MonthlyReconciliationBlockerType.MissingExpectedClose);
        blocker.Day.ShouldBe(6);
        blocker.AccountId.ShouldBe(accountId);
        blocker.AccountName.ShouldBe("Terminal sem fechamento");
        blocker.DailyCloseId.ShouldBeNull();
    }

    [Fact]
    public async Task Execute_ShouldListCancelledOnlyDayWithZeroVariance()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var counts = new List<MonthlyTransactionCountRow>
        {
            new(DateFrom.AddDays(14), TransactionStatus.Cancelled, 4)
        };

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], statusCounts: counts);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Days.Count.ShouldBe(31);
        var day = response.Days[14];
        day.Date.ShouldBe(DateFrom.AddDays(14));
        day.ActiveTransactionCount.ShouldBe(0);
        day.DraftTransactionCount.ShouldBe(0);
        day.CancelledTransactionCount.ShouldBe(4);
        day.NetVariance.ShouldBe(0m);
        day.Closes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldPassCallerBranchAndMonthToRepositories()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var otherBranchId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, productId: productId, closes: [], varianceRows: [], statusCounts: []);
        using var cancellation = new CancellationTokenSource();

        await CreateUseCase(ctx).Execute(BuildRequest(), cancellation.Token);

        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<int>(v => v == Year),
            Arg.Is<int>(v => v == Month),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.DailyCloseItemsRepository.Received(1).ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == productId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == DateFrom),
            Arg.Is<DateTime>(v => v == DateTo),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.TransactionsRepository.Received(1).CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<int>(v => v == Year),
            Arg.Is<int>(v => v == Month),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));
        await ctx.TransactionsRepository.Received(1)
            .ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                Arg.Is<Guid>(value => value == branchUser.BranchId),
                Arg.Is<int>(value => value == Year),
                Arg.Is<int>(value => value == Month),
                Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.TransactionsRepository.DidNotReceive()
            .CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                Arg.Is<Guid>(v => v == otherBranchId),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldExcludeCrossBranchCountsByUsingExactBranchAggregate()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var counts = new List<MonthlyTransactionCountRow>
        {
            new(DateFrom.AddDays(1), TransactionStatus.Active, 1)
        };

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], statusCounts: counts);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Days[1].ActiveTransactionCount.ShouldBe(1);
        response.Days.Sum(d => d.ActiveTransactionCount).ShouldBe(1);
    }

    [Fact]
    public async Task Execute_ShouldKeepVariancePerAccount_WhenMultipleClosesShareTheSameDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountA = new AccountBuilder().WithBranchId(branchUser.BranchId).WithName("Terminal A").Build();
        var accountB = new AccountBuilder().WithBranchId(branchUser.BranchId).WithName("Terminal B").Build();
        var date = DateFrom.AddDays(6);
        var closeA = BuildClose(branchUser.BranchId, accountA, date, DailyCloseStatus.Approved);
        var closeB = BuildClose(branchUser.BranchId, accountB, date, DailyCloseStatus.Approved);
        var rows = new List<VarianceTimeSeriesRow>
        {
            new(date, accountA.Id, accountA.Name, 10m, DailyCloseStatus.Approved),
            new(date, accountB.Id, accountB.Name, -25m, DailyCloseStatus.Approved)
        };

        var ctx = BuildContext(branchUser, closes: [closeA, closeB], varianceRows: rows, statusCounts: []);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        var day = response.Days[6];
        day.NetVariance.ShouldBe(-15m);
        day.Closes.Count.ShouldBe(2);
        day.Closes.Single(c => c.AccountId == accountA.Id).VarianceValue.ShouldBe(10m);
        day.Closes.Single(c => c.AccountId == accountB.Id).VarianceValue.ShouldBe(-25m);
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], statusCounts: []);

        var action = async () => await CreateUseCase(ctx).Execute(BuildRequest());

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidationException_WhenYearIsOutOfRange()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], statusCounts: []);
        var request = BuildRequest(year: 1900);

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            CreateUseCase(ctx).Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);
        await ctx.DailyClosesRepository.DidNotReceive().ListByBranchIdAndYearMonthAsNoTracking(
            Arg.Any<Guid>(),
            Arg.Any<int>(),
            Arg.Any<int>());
        await ctx.CashVarianceProductResolver.DidNotReceive().GetIdAsync(Arg.Any<Guid>());
        await ctx.DailyCloseItemsRepository.DidNotReceive()
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>());
        await ctx.TransactionsRepository.DidNotReceive()
            .CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    private static RequestMonthlyReconciliationJson BuildRequest(int year = Year, int month = Month)
    {
        return new RequestMonthlyReconciliationJsonBuilder()
            .WithYear(year)
            .WithMonth(month)
            .Build();
    }

    private static DailyClose BuildClose(Guid branchId, Account account, DateTime date, DailyCloseStatus status)
    {
        return new DailyCloseBuilder().WithBranchId(branchId).WithAccount(account).WithDate(date).WithStatus(status).Build();
    }

    private static GetMonthlyReconciliationUseCase CreateUseCase(TestContext ctx)
    {
        return new GetMonthlyReconciliationUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.TransactionsRepository,
            ctx.DailyCloseItemsRepository,
            ctx.CashVarianceProductResolver,
            new server.Application.Services.Settings.MonthLockReadinessEvaluator());
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        IReadOnlyList<DailyClose> closes,
        IReadOnlyList<VarianceTimeSeriesRow> varianceRows,
        IReadOnlyList<MonthlyTransactionCountRow> statusCounts,
        IReadOnlyList<TerminalActivityPairRow>? activityPairs = null,
        Guid? productId = null)
    {
        var resolvedProductId = productId ?? Guid.NewGuid();

        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = new DailyClosesRepositoryBuilder()
                .ListByBranchIdAndYearMonthAsNoTrackingReturns(branchUser.BranchId, Year, Month, closes)
                .Build(),
            TransactionsRepository = new TransactionsRepositoryBuilder()
                .CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTrackingReturns(
                    branchUser.BranchId, Year, Month, statusCounts)
                .ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTrackingReturns(
                    branchUser.BranchId, Year, Month, activityPairs ?? [])
                .Build(),
            DailyCloseItemsRepository = new DailyCloseItemsRepositoryBuilder()
                .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(
                    branchUser.BranchId, resolvedProductId, null, DateFrom, DateTo, varianceRows)
                .Build(),
            CashVarianceProductResolver = new CashVarianceProductResolverBuilder()
                .ReturnsId(branchUser.BranchId, resolvedProductId)
                .Build()
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IDailyCloseItemsRepository DailyCloseItemsRepository { get; init; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
    }
}
