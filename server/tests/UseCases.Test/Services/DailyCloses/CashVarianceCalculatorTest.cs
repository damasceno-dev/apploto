using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class CashVarianceCalculatorTest
{
    [Fact]
    public async Task CalculateAsync_ShouldUseZeroOpening_WhenNoPriorCloseExists()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 300m)],
            priorClose: null);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(300m);
        await ctx.DailyClosesRepository.Received(1)
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Is<Guid>(id => id == ctx.BranchId),
                Arg.Is<Guid>(id => id == ctx.AccountId),
                Arg.Is<DateTime>(date => date == ctx.BranchLocalDate));
    }

    [Fact]
    public async Task CalculateAsync_ShouldUseMostRecentPriorCloseItemsOnly_WhenPriorCloseExists()
    {
        var cashVarianceProductId = Guid.NewGuid();
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 1_000m)],
            priorClose: null,
            cashVarianceProductId: cashVarianceProductId);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [
                Item(Guid.NewGuid(), 250m),
                Item(cashVarianceProductId, 9_999m),
                Item(Guid.NewGuid(), 100m, active: false)
            ]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(750m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldUsePriorCloseThreeDaysAgo_WhenInterveningDaysAreSkipped()
    {
        var priorDate = new DateTime(2026, 4, 27);
        var ctx = BuildContext(
            branchLocalDate: priorDate.AddDays(3),
            currentItems: [Item(Guid.NewGuid(), 200m)],
            priorClose: null);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            priorDate,
            [Item(Guid.NewGuid(), 125m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(75m);
        await ctx.DailyClosesRepository.Received(1)
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                Arg.Is<Guid>(id => id == ctx.BranchId),
                Arg.Is<Guid>(id => id == ctx.AccountId),
                Arg.Is<DateTime>(date => date == ctx.BranchLocalDate));
    }

    [Fact]
    public async Task CalculateAsync_ShouldSubtractTransactionsInMinusOut()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 1_000m)],
            priorClose: null,
            transactionsIn: 250m,
            transactionsOut: 40m);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(790m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldQueryOnlyRequestedAccount_WhenSiblingAccountHasDifferentSums()
    {
        var siblingAccountId = Guid.NewGuid();
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 500m)],
            priorClose: null,
            transactionsIn: 200m,
            transactionsOut: 50m);
        ctx.TransactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                ctx.BranchId,
                siblingAccountId,
                ctx.BranchLocalDate,
                Direction.In)
            .Returns(9_000m);
        ctx.TransactionsRepository
            .SumActiveValueByAccountAndDateAsNoTracking(
                ctx.BranchId,
                siblingAccountId,
                ctx.BranchLocalDate,
                Direction.Out)
            .Returns(4_000m);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(350m);
        await ctx.TransactionsRepository.Received(1)
            .SumActiveValueByAccountAndDateAsNoTracking(
                Arg.Is<Guid>(id => id == ctx.BranchId),
                Arg.Is<Guid>(id => id == ctx.AccountId),
                Arg.Is<DateTime>(date => date == ctx.BranchLocalDate),
                Arg.Is<Direction?>(direction => direction == Direction.In));
        await ctx.TransactionsRepository.Received(1)
            .SumActiveValueByAccountAndDateAsNoTracking(
                Arg.Is<Guid>(id => id == ctx.BranchId),
                Arg.Is<Guid>(id => id == ctx.AccountId),
                Arg.Is<DateTime>(date => date == ctx.BranchLocalDate),
                Arg.Is<Direction?>(direction => direction == Direction.Out));
        await ctx.TransactionsRepository.DidNotReceive()
            .SumActiveValueByAccountAndDateAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Is<Guid>(id => id == siblingAccountId),
                Arg.Any<DateTime>(),
                Arg.Any<Direction?>());
    }

    [Fact]
    public async Task CalculateAsync_ShouldUseActiveOnlyTransactionSums_FromRepositoryContract()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 500m)],
            priorClose: null,
            transactionsIn: 80m,
            transactionsOut: 30m);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(450m);
        await ctx.TransactionsRepository.Received(1)
            .SumActiveValueByAccountAndDateAsNoTracking(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                Direction.In);
        await ctx.TransactionsRepository.Received(1)
            .SumActiveValueByAccountAndDateAsNoTracking(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                Direction.Out);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReturnPositiveVariance_ForTypicalMixedValues()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 1_000m)],
            priorClose: null,
            transactionsIn: 400m,
            transactionsOut: 150m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 700m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(50m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldReturnNegativeVariance_ForTypicalMixedValues()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 800m)],
            priorClose: null,
            transactionsIn: 300m,
            transactionsOut: 50m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 700m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(-150m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldExcludeCashVarianceProduct_FromClosingAndOpening()
    {
        var cashVarianceProductId = Guid.NewGuid();
        var ctx = BuildContext(
            currentItems:
            [
                Item(Guid.NewGuid(), 100m),
                Item(cashVarianceProductId, 9_999m)
            ],
            priorClose: null,
            cashVarianceProductId: cashVarianceProductId);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [
                Item(Guid.NewGuid(), 70m),
                Item(cashVarianceProductId, 8_888m)
            ]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(30m);
    }

    private static CalculatorContext BuildContext(
        IReadOnlyList<DailyCloseItem> currentItems,
        DailyClose? priorClose,
        decimal transactionsIn = 0m,
        decimal transactionsOut = 0m,
        Guid? branchId = null,
        Guid? accountId = null,
        DateTime? branchLocalDate = null,
        Guid? currentDailyCloseId = null,
        Guid? cashVarianceProductId = null)
    {
        var ctx = new CalculatorContext
        {
            BranchId = branchId ?? Guid.NewGuid(),
            AccountId = accountId ?? Guid.NewGuid(),
            BranchLocalDate = branchLocalDate ?? new DateTime(2026, 4, 30),
            CurrentDailyCloseId = currentDailyCloseId ?? Guid.NewGuid(),
            CashVarianceProductId = cashVarianceProductId ?? Guid.NewGuid(),
            CurrentItems = currentItems,
            PriorClose = priorClose
        };

        ctx.DailyCloseItemsRepository = new DailyCloseItemsRepositoryBuilder()
            .ListActiveByDailyCloseIdAsNoTrackingReturns(ctx.CurrentDailyCloseId, ctx.CurrentItems)
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTrackingReturns(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                ctx.PriorClose)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .SumActiveValueByAccountAndDateAsNoTrackingReturns(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                Direction.In,
                transactionsIn)
            .SumActiveValueByAccountAndDateAsNoTrackingReturns(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                Direction.Out,
                transactionsOut)
            .Build();
        ctx.Calculator = new CashVarianceCalculator(
            ctx.DailyCloseItemsRepository,
            ctx.DailyClosesRepository,
            ctx.TransactionsRepository);

        return ctx;
    }

    private static void RewirePriorClose(CalculatorContext ctx)
    {
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTrackingReturns(
                ctx.BranchId,
                ctx.AccountId,
                ctx.BranchLocalDate,
                ctx.PriorClose)
            .Build();
        ctx.Calculator = new CashVarianceCalculator(
            ctx.DailyCloseItemsRepository,
            ctx.DailyClosesRepository,
            ctx.TransactionsRepository);
    }

    private static DailyCloseItem Item(Guid productId, decimal value, bool active = true)
    {
        return new DailyCloseItemBuilder()
            .WithProductId(productId)
            .WithValue(value)
            .WithActive(active)
            .Build();
    }

    private static DailyClose PriorClose(
        Guid branchId,
        Guid accountId,
        DateTime date,
        IReadOnlyList<DailyCloseItem> items)
    {
        return new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date)
            .WithItems(items)
            .Build();
    }

    private sealed class CalculatorContext
    {
        public Guid BranchId { get; set; }
        public Guid AccountId { get; set; }
        public DateTime BranchLocalDate { get; set; }
        public Guid CurrentDailyCloseId { get; set; }
        public Guid CashVarianceProductId { get; set; }
        public IReadOnlyList<DailyCloseItem> CurrentItems { get; set; } = [];
        public DailyClose? PriorClose { get; set; }
        public IDailyCloseItemsRepository DailyCloseItemsRepository { get; set; } = null!;
        public IDailyClosesRepository DailyClosesRepository { get; set; } = null!;
        public ITransactionsRepository TransactionsRepository { get; set; } = null!;
        public CashVarianceCalculator Calculator { get; set; } = null!;

        public Task<decimal> CalculateAsync()
        {
            return Calculator.CalculateAsync(
                BranchId,
                AccountId,
                BranchLocalDate,
                CurrentDailyCloseId,
                CashVarianceProductId,
                CancellationToken.None);
        }
    }
}
