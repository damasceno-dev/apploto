using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using server.Application.Services.DailyCloses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

/// <summary>
/// M9.5 Phase 1 (items 1.1–1.4) — characterization, not a decision. Each case runs the production
/// <see cref="CashVarianceCalculator"/> (§6.12: variance = Δcounted − (In − Out) on the Terminal) over one Terminal
/// day with an opening of cash R$ 1,000 and counted stock R$ 1,000, and a value V = R$ 5 that changes hands.
/// <list type="bullet">
/// <item>1.1 and 1.2 pin the two representations that are each internally consistent (count-only versus
/// product-transaction-explained). Count-only is canonical under working answer Q5, pending the shop answer to question 5.
/// The second remains a consistent alternative, not the adopted workflow.</item>
/// <item>1.3 pins the double-count failure mode, wrong under either representation.</item>
/// <item>1.4 pins the fiado divergence for rows with no Terminal counterpart. The M7.7 Phase 5.6 commands now write
/// the counterpart for a counted-stock sale and a counted-cash repayment (see
/// <c>FiadoReconciliationCoherenceTest</c>), so these inputs are built directly here to characterize historical,
/// imported or non-counted operations; the commands are not weakened to reproduce them. Contrast with Access:
/// <c>AtualizarDiferenca</c> added the fiado balance (<c>TxtFiado</c>/<c>TxtFiadoIni</c>) to the conferência totals, so
/// these cases netted to zero there; the port keeps fiado out of §6.12 and the signed 1.3 amendment answers the
/// physical cases with linked Terminal legs instead.</item>
/// </list>
/// </summary>
public class CashVarianceRepresentationCharacterizationTest
{
    private const decimal V = 5m;
    private const decimal Opening = 1_000m;

    [Fact]
    public async Task Item11_CountOnly_StockDownAndCashUpWithNoLedgerRows_ShouldBeZero()
    {
        var variance = await Calculate(stockClosing: Opening - V, cashClosing: Opening + V);

        variance.ShouldBe(0m);
    }

    [Fact]
    public async Task Item12_ProductTransactionExplained_OutRowWithStockDownAndProceedsNotCounted_ShouldBeZero()
    {
        var variance = await Calculate(stockClosing: Opening - V, cashClosing: Opening, terminalOut: V);

        variance.ShouldBe(0m);
    }

    [Fact]
    public async Task Item13_DoubleCount_SwapCountedPlusAnOutRow_ShouldBePlusV()
    {
        var variance = await Calculate(stockClosing: Opening - V, cashClosing: Opening + V, terminalOut: V);

        variance.ShouldBe(+V);
    }

    [Fact]
    public async Task Item14a_FiadoCashReceiptWithoutTerminalCounterpart_ShouldBePlusV()
    {
        // The Tab In of V lives on the Tab account, outside the Terminal's §6.12 inputs; the drawer counts +V.
        var variance = await Calculate(stockClosing: Opening, cashClosing: Opening + V);

        variance.ShouldBe(+V);
    }

    [Fact]
    public async Task Item14b_CountedStockFiadoSaleWithoutTerminalCounterpart_ShouldBeMinusV()
    {
        // The Tab Out of V lives on the Tab account; counted stock leaves the Terminal with no Terminal row.
        var variance = await Calculate(stockClosing: Opening - V, cashClosing: Opening);

        variance.ShouldBe(-V);
    }

    [Fact]
    public async Task Item14_WithTheSignedPhysicalCounterparts_BothFiadoCasesReconcileAtZero()
    {
        // The signed M7.7 1.3 amendment, through the same formula: generated Terminal In V / Terminal Out V.
        (await Calculate(stockClosing: Opening, cashClosing: Opening + V, terminalIn: V)).ShouldBe(0m);
        (await Calculate(stockClosing: Opening - V, cashClosing: Opening, terminalOut: V)).ShouldBe(0m);
    }

    [Theory]
    [InlineData(120, 100, 0, 140)]
    [InlineData(0, 0, 350, 350)]
    [InlineData(120, 100, 350, 490)]
    public async Task OpeningSideInflows_OldRepresentationDiverges_CorrectedRepresentationReconciles(
        decimal feeToday, decimal feePrevious, decimal operatorToday, decimal oldVariance)
    {
        // Explicit model: opening physical assets = 2,000, only ordinary Out rows = 40.
        // Today's actual fee and Operador enter physical cash. Yesterday's fee is already
        // inside opening cash: it is not an additional carried asset. Q3/Q9 are assumptions.
        // Access DSum ignored sign; positive/misclassified/test-like export rows mean this
        // example cannot establish parity for all historical Access days.
        const decimal ordinaryOut = 40m;
        var cashClosing = Opening + feeToday + operatorToday - ordinaryOut;
        var physicalClosing = Opening + cashClosing;
        var modeledAccess = physicalClosing + ordinaryOut - (2 * Opening + feeToday + operatorToday);
        modeledAccess.ShouldBe(0m);

        (await Calculate(Opening, cashClosing, terminalOut: ordinaryOut,
            countedFeeToday: feeToday, countedFeePrevious: feePrevious)).ShouldBe(oldVariance);
        (await Calculate(Opening, cashClosing, terminalIn: feeToday + operatorToday,
            terminalOut: ordinaryOut)).ShouldBe(0m);

        // The preceding day also records its own fee as In, never as a count slot.
        (await Calculate(Opening, Opening + feePrevious, terminalIn: feePrevious)).ShouldBe(0m);
    }

    [Fact]
    public async Task FirstCorrectedDay_WithRetainedHistoricalFeeOpening_ShouldExposeTransitionDifference()
    {
        // Retirement does not rewrite prior DailyCloseItems. A prior extra fee slot of 100
        // still contributes to opening, so migration alone cannot certify this transition zero.
        (await Calculate(Opening, Opening + 120m, terminalIn: 120m,
            countedFeePrevious: 100m)).ShouldBe(-100m);
    }

    private static async Task<decimal> Calculate(
        decimal stockClosing,
        decimal cashClosing,
        decimal terminalIn = 0m,
        decimal terminalOut = 0m,
        decimal countedFeeToday = 0m,
        decimal countedFeePrevious = 0m)
    {
        var branchId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var date = new DateTime(2026, 9, 15);
        var currentCloseId = Guid.NewGuid();
        var stockProductId = Guid.NewGuid();
        var cashProductId = Guid.NewGuid();
        var feeProductId = Guid.NewGuid();
        var cashVarianceProductId = Guid.NewGuid();

        var opening = new DailyCloseBuilder()
            .WithBranchId(branchId)
            .WithAccountId(accountId)
            .WithDate(date.AddDays(-1))
            .WithItems([Item(stockProductId, Opening), Item(cashProductId, Opening), Item(feeProductId, countedFeePrevious)])
            .Build();
        var calculator = new CashVarianceCalculator(
            new DailyCloseItemsRepositoryBuilder()
                .ListActiveByDailyCloseIdAsNoTrackingReturns(
                    currentCloseId,
                    [Item(stockProductId, stockClosing), Item(cashProductId, cashClosing), Item(feeProductId, countedFeeToday)])
                .Build(),
            new DailyClosesRepositoryBuilder().Build(),
            new TransactionsRepositoryBuilder()
                .SumActiveValueByAccountAndDateAsNoTrackingReturns(branchId, accountId, date, Direction.In, terminalIn)
                .SumActiveValueByAccountAndDateAsNoTrackingReturns(branchId, accountId, date, Direction.Out, terminalOut)
                .Build());

        return await calculator.CalculateWithOpeningSourceAsync(
            branchId,
            accountId,
            date,
            currentCloseId,
            cashVarianceProductId,
            opening,
            CancellationToken.None);
    }

    private static DailyCloseItem Item(Guid productId, decimal value)
    {
        return new DailyCloseItemBuilder().WithProductId(productId).WithValue(value).Build();
    }
}
