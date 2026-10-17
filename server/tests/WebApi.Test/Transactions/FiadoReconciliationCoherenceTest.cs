using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 5.4 coherence, end to end through the shipped commands and reads and the production cash-variance
/// calculator (variance preview and Submit): the signed R$ 5 physical cases reconcile at zero, a non-cash repayment
/// moves only the Tab, and repayments across Tabs keep statement, balance and FIFO aging in agreement.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class FiadoReconciliationCoherenceTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CountedStockSale_ShouldReconcileAtZero_WhenStockIsCountedAt995()
    {
        var (ctx, products) = await SeedWithOpeningAsync("FiadoRecStock");

        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: true), ctx.Token);

        // Stock 1000 → 995 and the generated Terminal Out 5: zero difference; the Tab's R$ 5 is not in the formula.
        (await Preview(ctx, products, stock: 995m, cash: 1000m)).ShouldBe(0m);
        (await SubmitCounts(ctx, products, stock: 995m, cash: 1000m)).ShouldBe(0m);
        (await Statement(ctx)).OutstandingTotal.ShouldBe(5m);
    }

    [Fact]
    public async Task CashRepayment_ShouldReconcileAtZero_WhenCashIsCountedAt1005()
    {
        var (ctx, products) = await SeedWithOpeningAsync("FiadoRecCash");
        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: false), ctx.Token);

        await Created("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.CashMethodId, 5m), ctx.Token);

        (await Preview(ctx, products, stock: 1000m, cash: 1005m)).ShouldBe(0m);
        (await SubmitCounts(ctx, products, stock: 1000m, cash: 1005m)).ShouldBe(0m);
        var statement = await Statement(ctx);
        statement.OutstandingTotal.ShouldBe(0m);
        statement.OldestUnpaidDate.ShouldBeNull();
    }

    [Fact]
    public async Task NonCashRepayment_ShouldMoveOnlyTheTab_WithNoTerminalLegOrCountChange()
    {
        var (ctx, products) = await SeedWithOpeningAsync("FiadoRecPix");
        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 20m, handsOutCountedStock: false), ctx.Token);

        var payment = await Created(
            "/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.NonCashMethodId, 5m), ctx.Token);

        payment.GeneratedTransactionId.ShouldBeNull();
        (await ctx.ListBranchTransactionsAsync()).ShouldNotContain(row => row.AccountId == ctx.TerminalId);
        (await Preview(ctx, products, stock: 1000m, cash: 1000m)).ShouldBe(0m);
        (await Statement(ctx)).OutstandingTotal.ShouldBe(15m);
    }

    [Fact]
    public async Task PhysicalCounterparts_ShouldBeTheOnlyReasonTheCountsReconcile()
    {
        // Control: the same R$ 5 counts WITHOUT the generated legs leave the signed ±5 difference the 1.3 amendment
        // exists to remove (an ordinary fiado sale and a non-cash repayment write no Terminal row).
        var (ctx, products) = await SeedWithOpeningAsync("FiadoRecControl");
        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: false), ctx.Token);

        (await Preview(ctx, products, stock: 995m, cash: 1000m)).ShouldBe(-5m);
        (await Preview(ctx, products, stock: 1000m, cash: 1005m)).ShouldBe(5m);
    }

    [Fact]
    public async Task Repayments_ShouldKeepStatementBalanceAndAgingInAgreement_AcrossTabs()
    {
        var (ctx, _) = await SeedWithOpeningAsync("FiadoRecTabs");
        var otherTab = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Tab, "Fiado balcão");
        // Same-day settlement types derive DueDate from Date, so distinct sale dates fix the FIFO order (a tie would
        // fall back to Id order). Tab rows need no open close, so earlier dates are fine.
        var firstSale = await Created("/transaction", Sale(ctx, ctx.TabId, 100m, ctx.Today.AddDays(-2)), ctx.Token);
        var secondSale = await Created("/transaction", Sale(ctx, otherTab.Id, 50m, ctx.Today.AddDays(-1)), ctx.Token);

        // Partial payment on the other Tab settles the branch-wide oldest-due sale first.
        var partial = await Created("/transaction/fiado-payment", Payment(ctx, otherTab.Id, 60m), ctx.Token);
        await AssertCoherent(ctx, expectedOutstanding: 90m, expectedAging: [(firstSale.Id, 40m), (secondSale.Id, 50m)]);
        (await Aging(ctx)).Items.Select(item => item.TransactionId).ShouldNotContain(partial.Id);

        // Full settlement, then an overpayment that becomes credit with no aged debt.
        await Created("/transaction/fiado-payment", Payment(ctx, ctx.TabId, 90m), ctx.Token);
        await AssertCoherent(ctx, expectedOutstanding: 0m, expectedAging: []);
        await Created("/transaction/fiado-payment", Payment(ctx, otherTab.Id, 10m), ctx.Token);
        await AssertCoherent(ctx, expectedOutstanding: -10m, expectedAging: []);
        var statement = await Statement(ctx);
        statement.Items.Count.ShouldBe(5);
        statement.Items[0].RunningBalance.ShouldBe(-10m);
        statement.OldestUnpaidDate.ShouldBeNull();
    }

    private async Task AssertCoherent(
        FiadoLinkedMovementTestContext ctx,
        decimal expectedOutstanding,
        (Guid Id, decimal Value)[] expectedAging)
    {
        var statement = await Statement(ctx);
        var aging = await Aging(ctx);
        var balanceResponse = await _client.GetAuthAsync($"/report/fiado/balance?clientId={ctx.ClientId}", ctx.Token);
        var balance = await balanceResponse.ReadContentAsync<ResponseFiadoBalanceJson>();

        statement.OutstandingTotal.ShouldBe(expectedOutstanding);
        statement.ClosingBalance.ShouldBe(expectedOutstanding);
        balance.TotalOutstanding.ShouldBe(expectedOutstanding);
        aging.Items.Select(item => (item.TransactionId, item.Value)).ShouldBe(expectedAging);
        // A debtor's aged remainders add up to the outstanding total; credit ages nothing.
        aging.Items.Sum(item => item.Value).ShouldBe(Math.Max(0m, expectedOutstanding));
    }

    private async Task<(FiadoLinkedMovementTestContext Ctx, ReconciliationProducts Products)> SeedWithOpeningAsync(string label)
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, label, Role.Manager);
        var stock = await factory.SeedProductAsync(ctx.BranchId, "Raspadinha", displayOrder: 1);
        var cash = await factory.SeedProductAsync(ctx.BranchId, "Dinheiro", displayOrder: 2);
        var variance = await factory.SeedProductAsync(ctx.BranchId, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        // Yesterday's counted close is the opening source: stock 1000, cash 1000.
        var yesterday = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.TerminalId, ctx.Today.AddDays(-1), DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(yesterday.Id, stock.Id, 1000m);
        await factory.SeedDailyCloseItemAsync(yesterday.Id, cash.Id, 1000m);
        return (ctx, new ReconciliationProducts(stock.Id, cash.Id, variance.Id));
    }

    private async Task<decimal> Preview(FiadoLinkedMovementTestContext ctx, ReconciliationProducts products, decimal stock, decimal cash)
    {
        var response = await _client.PostAuthAsync(
            $"/dailyclose/{ctx.CloseId}/variance-preview",
            new RequestDailyCloseVariancePreviewJson { Items = Items(products, stock, cash) },
            ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseDailyCloseVariancePreviewJson>()).CashVariance;
    }

    private async Task<decimal> SubmitCounts(FiadoLinkedMovementTestContext ctx, ReconciliationProducts products, decimal stock, decimal cash)
    {
        var close = (await factory.ReloadAsync<DailyClose>(ctx.CloseId))!;
        var put = await _client.PutAuthAsync(
            $"/dailyclose/{ctx.CloseId}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = close.Version, Items = Items(products, stock, cash) },
            ctx.Token);
        put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
        var submit = await _client.PostAuthAsync($"/dailyclose/{ctx.CloseId}/submit", ctx.Token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(ctx.CloseId);
        // Counts stay exactly as the operator entered them; only the official variance row is computed.
        items.Single(item => item.ProductId == products.Stock).Value.ShouldBe(stock);
        items.Single(item => item.ProductId == products.Cash).Value.ShouldBe(cash);
        return items.Single(item => item.ProductId == products.Variance).Value;
    }

    private static IReadOnlyList<RequestUpsertDailyCloseItemJson> Items(ReconciliationProducts products, decimal stock, decimal cash)
    {
        return
        [
            new RequestUpsertDailyCloseItemJson { ProductId = products.Stock, Value = stock },
            new RequestUpsertDailyCloseItemJson { ProductId = products.Cash, Value = cash }
        ];
    }

    private async Task<ResponseClientFiadoStatementJson> Statement(FiadoLinkedMovementTestContext ctx)
    {
        var response = await _client.GetAuthAsync($"/client/{ctx.ClientId}/fiado-statement", ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseClientFiadoStatementJson>();
    }

    private async Task<ResponseFiadoAgingJson> Aging(FiadoLinkedMovementTestContext ctx)
    {
        var response = await _client.GetAuthAsync($"/report/fiado/aging?clientId={ctx.ClientId}&page=1&pageSize=50", ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseFiadoAgingJson>();
    }

    private async Task<ResponseCreateTransactionJson> Created<TRequest>(string url, TRequest request, string token)
    {
        var response = await _client.PostAuthAsync(url, request, token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private static RequestCreateTransactionJson Sale(FiadoLinkedMovementTestContext ctx, Guid tabId, decimal value, DateTime date)
    {
        return new RequestCreateTransactionJson
        {
            Date = date, Value = value, TransactionTypeId = ctx.SaleTypeId, AccountId = tabId, ClientId = ctx.ClientId
        };
    }

    private static RequestReceiveFiadoPaymentJson Payment(FiadoLinkedMovementTestContext ctx, Guid tabId, decimal value)
    {
        return new RequestReceiveFiadoPaymentJson
        {
            Date = ctx.Today, Value = value, TransactionTypeId = ctx.NonCashMethodId, AccountId = tabId, ClientId = ctx.ClientId
        };
    }

    private sealed record ReconciliationProducts(Guid Stock, Guid Cash, Guid Variance);
}
