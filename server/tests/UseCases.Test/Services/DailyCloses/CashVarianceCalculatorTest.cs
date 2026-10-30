using CommonTestUtilities.Services;
using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
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
            .GetCountedPredecessorAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == ctx.CurrentDailyCloseId),
                Arg.Any<CancellationToken>());
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
            .GetCountedPredecessorAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == ctx.CurrentDailyCloseId),
                Arg.Any<CancellationToken>());
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
    public async Task CalculateAsync_ShouldUseZeroTransactions_WhenNoTransactionsExist()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 250m)],
            priorClose: null);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 150m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(100m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldSubtractOnlyInTransactions_WhenOnlyInExists()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 500m)],
            priorClose: null,
            transactionsIn: 75m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 100m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(325m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldAddBackOutTransactions_WhenOnlyOutExists()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 500m)],
            priorClose: null,
            transactionsOut: 75m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 100m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(475m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldPreserveDecimalPrecision_ForLargeValues()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 9_999_999_999.99m)],
            priorClose: null,
            transactionsIn: 123_456_789.12m,
            transactionsOut: 23_456_789.01m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(Guid.NewGuid(), 8_765_432_100.10m)]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(1_134_567_899.78m);
    }

    [Fact]
    public async Task CalculateAsync_ShouldSumOnlyTheSessionsOwnRows_WhenAnotherSessionOfTheSameCashboxHasRows()
    {
        var ctx = BuildContext(
            currentItems: [Item(Guid.NewGuid(), 500m)],
            priorClose: null,
            transactionsIn: 200m,
            transactionsOut: 50m);
        var siblingSessionId = Guid.NewGuid();
        ctx.TransactionsRepository
            .SumActiveValueBySessionAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == siblingSessionId),
                Arg.Any<Direction?>(),
                Arg.Any<CancellationToken>())
            .Returns(9_000m);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(350m);
        await ctx.TransactionsRepository.DidNotReceive()
            .SumActiveValueBySessionAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == siblingSessionId),
                Arg.Any<Direction?>(),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CalculateAsync_ShouldUseAcknowledgedOpening_InsteadOfThePredecessorClosing()
    {
        var productId = Guid.NewGuid();
        var ctx = BuildContext(
            currentItems: [Item(productId, 1_100m)],
            priorClose: null);
        ctx.PriorClose = PriorClose(ctx.BranchId, ctx.AccountId, ctx.BranchLocalDate, [Item(productId, 1_100m)]);
        ctx.Opening = new SessionOpening
        {
            SessionId = ctx.CurrentDailyCloseId,
            Items = [new SessionOpeningItem { ProductId = productId, ProposedValue = 1_100m, AcknowledgedValue = 1_080m }]
        };
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        // E11b: Finished 1,100 → Start 1,080; the acknowledged 1,080 is TotalOpening (1,100 − 1,080 − 0 = 20).
        result.ShouldBe(20m);
        await ctx.DailyClosesRepository.DidNotReceive()
            .GetCountedPredecessorAsNoTracking(Arg.Any<DailyClose>(), Arg.Any<CancellationToken>());
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
            .SumActiveValueBySessionAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == ctx.CurrentDailyCloseId),
                Direction.In,
                Arg.Any<CancellationToken>());
        await ctx.TransactionsRepository.Received(1)
            .SumActiveValueBySessionAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == ctx.CurrentDailyCloseId),
                Direction.Out,
                Arg.Any<CancellationToken>());
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

    /// <summary>
    /// Access-faithful day: opening counts R$5,800 (cash 5,000 + stock 800); the day records
    /// R$3,000 of Out rows (cash-to-bank deposit, PIX cash-out, payout), so counted assets are
    /// expected to drop to R$2,800; the operator counts R$2,780 — the drawer is R$20 short.
    /// Pins the §6.12 sign convention against a realistic scenario, mirroring the original
    /// FrmCaixa conferência where all-Negativo terminal days balanced as Lançamentos + Final − Inicial.
    /// </summary>
    [Fact]
    public async Task CalculateAsync_ShouldReturnMinus20_ForRealisticAllOutTerminalDay()
    {
        var ctx = BuildContext(
            currentItems:
            [
                Item(Guid.NewGuid(), 1_980m),
                Item(Guid.NewGuid(), 500m),
                Item(Guid.NewGuid(), 300m)
            ],
            priorClose: null,
            transactionsOut: 3_000m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [
                Item(Guid.NewGuid(), 5_000m),
                Item(Guid.NewGuid(), 500m),
                Item(Guid.NewGuid(), 300m)
            ]);
        RewirePriorClose(ctx);

        var result = await ctx.CalculateAsync();

        result.ShouldBe(-20m);
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

    [Fact]
    public async Task CalculateCandidateAsync_ShouldUseCandidateValuesInsteadOfSavedDraftItems()
    {
        var candidateProductId = Guid.NewGuid();
        var ctx = BuildContext(
            currentItems: [Item(candidateProductId, 10m)],
            priorClose: null,
            transactionsIn: 20m,
            transactionsOut: 5m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(candidateProductId, 50m)]);
        RewirePriorClose(ctx);

        var result = await ctx.Calculator.CalculateCandidateAsync(
            ctx.Session,
            [new RequestUpsertDailyCloseItemJson { ProductId = candidateProductId, Value = 200m }],
            ctx.CashVarianceProductId,
            CancellationToken.None);

        result.ShouldBe(135m);
        await ctx.DailyCloseItemsRepository.DidNotReceive()
            .ListActiveByDailyCloseIdAsNoTracking(Arg.Any<Guid>());
    }

    [Fact]
    public async Task CalculateCandidateAsync_ShouldMatchSavedCalculation_WhenValuesAreTheSame()
    {
        var firstProductId = Guid.NewGuid();
        var secondProductId = Guid.NewGuid();
        var currentItems =
            new[]
            {
                Item(firstProductId, 120m),
                Item(secondProductId, 30m)
            };
        var ctx = BuildContext(
            currentItems,
            priorClose: null,
            transactionsIn: 25m,
            transactionsOut: 10m);
        ctx.PriorClose = PriorClose(
            ctx.BranchId,
            ctx.AccountId,
            ctx.BranchLocalDate.AddDays(-1),
            [Item(firstProductId, 80m), Item(secondProductId, 20m)]);
        RewirePriorClose(ctx);

        var savedResult = await ctx.CalculateAsync();
        var candidateResult = await ctx.Calculator.CalculateCandidateAsync(
            ctx.Session,
            [
                new RequestUpsertDailyCloseItemJson { ProductId = firstProductId, Value = 120m },
                new RequestUpsertDailyCloseItemJson { ProductId = secondProductId, Value = 30m }
            ],
            ctx.CashVarianceProductId,
            CancellationToken.None);

        candidateResult.ShouldBe(savedResult);
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
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .SumActiveValueBySessionAsNoTrackingReturns(ctx.CurrentDailyCloseId, Direction.In, transactionsIn)
            .SumActiveValueBySessionAsNoTrackingReturns(ctx.CurrentDailyCloseId, Direction.Out, transactionsOut)
            .Build();
        RewirePriorClose(ctx);

        return ctx;
    }

    private static void RewirePriorClose(CalculatorContext ctx)
    {
        ctx.Session = new DailyCloseBuilder()
            .WithId(ctx.CurrentDailyCloseId)
            .WithBranchId(ctx.BranchId)
            .WithAccountId(ctx.AccountId)
            .WithDate(ctx.BranchLocalDate)
            .Build();
        if (ctx.Opening is not null)
            ctx.Session = new DailyCloseBuilder().WithId(ctx.CurrentDailyCloseId).WithBranchId(ctx.BranchId).WithAccountId(ctx.AccountId).WithDate(ctx.BranchLocalDate).WithOpening(ctx.Opening).Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetCountedPredecessorAsNoTrackingReturns(ctx.CurrentDailyCloseId, ctx.PriorClose)
            .Build();
        var cashVarianceProductResolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(ctx.BranchId, ctx.CashVarianceProductId)
            .Build();
        ctx.Calculator = new CashVarianceCalculator(
            ctx.DailyCloseItemsRepository,
            SessionTestServices.OpeningResolver(ctx.DailyClosesRepository, cashVarianceProductResolver),
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
        public SessionOpening? Opening { get; set; }
        public DailyClose Session { get; set; } = null!;
        public IDailyCloseItemsRepository DailyCloseItemsRepository { get; set; } = null!;
        public IDailyClosesRepository DailyClosesRepository { get; set; } = null!;
        public ITransactionsRepository TransactionsRepository { get; set; } = null!;
        public CashVarianceCalculator Calculator { get; set; } = null!;

        public Task<decimal> CalculateAsync()
        {
            return Calculator.CalculateAsync(Session, CashVarianceProductId, CancellationToken.None);
        }
    }
}
