using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Transactions;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M7.7 Phase 5.8: the operator summary keeps counting generated Terminal legs under their initiating operator and
/// reports that automatic share overall and per category, as a subset of the unchanged totals.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerOperatorSummaryAutomaticShareTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Summary_ShouldReportTheAutomaticShare_InsideTheEstablishedTotals()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "SummaryAutoShare", Role.Manager);
        var otherOperator = await factory.SeedOperatorAsync(ctx.BranchId, "Outro operador");
        var expenses = await factory.SeedCategoryAsync(ctx.BranchId, "Despesas", Direction.Out);
        var expenseType = await factory.SeedTransactionTypeAsync(expenses.Id, "Outras despesas");

        // The initiating operator: one counted-stock sale and one cash repayment (R$ 5 each, both paired), one
        // non-cash repayment, and one ordinary Terminal expense.
        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: true), ctx.Token);
        await Created("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.CashMethodId, 5m), ctx.Token);
        await Created("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.NonCashMethodId, 30m), ctx.Token);
        await Created(
            "/transaction",
            new RequestCreateTransactionJson { Date = ctx.Today, Value = 12m, TransactionTypeId = expenseType.Id, AccountId = ctx.TerminalId },
            ctx.Token);
        // Excluded: a Draft pair, a cancelled pair, and another operator's pair.
        await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 100m, handsOutCountedStock: true, saveAsDraft: true), ctx.Token);
        var cancelled = await Created("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 200m, handsOutCountedStock: true), ctx.Token);
        var cancel = await _client.PostAuthAsync(
            $"/transaction/{cancelled.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Lançada por engano" },
            ctx.Token,
            expectedVersion: cancelled.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var otherSale = TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 70m, handsOutCountedStock: true);
        await Created(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = otherSale.Date, Value = otherSale.Value, TransactionTypeId = otherSale.TransactionTypeId,
                AccountId = otherSale.AccountId, ClientId = otherSale.ClientId, DueDate = otherSale.DueDate,
                HandsOutCountedStock = true, RecordedByOperatorId = otherOperator.Id
            },
            ctx.Token);

        var summary = await Summary(ctx.OperatorId, ctx);
        var otherSummary = await Summary(otherOperator.Id, ctx);

        summary.TotalTransactionCount.ShouldBe(6);
        summary.TotalInValue.ShouldBe(5m + 5m + 30m);
        summary.TotalOutValue.ShouldBe(5m + 5m + 12m);
        summary.NetValue.ShouldBe(18m);
        summary.GeneratedTransactionCount.ShouldBe(2);
        summary.GeneratedInValue.ShouldBe(5m);
        summary.GeneratedOutValue.ShouldBe(5m);
        summary.ByCategory.Sum(category => category.Count).ShouldBe(summary.TotalTransactionCount);
        summary.ByCategory.Sum(category => category.GeneratedCount).ShouldBe(summary.GeneratedTransactionCount);
        summary.ByCategory.Sum(category => category.GeneratedIn).ShouldBe(summary.GeneratedInValue);
        summary.ByCategory.Sum(category => category.GeneratedOut).ShouldBe(summary.GeneratedOutValue);

        var stockCategory = summary.ByCategory.Single(category => category.CategoryName == "Movimentos automáticos de fiado (saída)");
        stockCategory.Count.ShouldBe(1);
        stockCategory.TotalOut.ShouldBe(5m);
        stockCategory.GeneratedCount.ShouldBe(1);
        stockCategory.GeneratedOut.ShouldBe(5m);
        var cashCategory = summary.ByCategory.Single(category => category.CategoryName == "Movimentos automáticos de fiado (entrada)");
        cashCategory.TotalIn.ShouldBe(5m);
        cashCategory.GeneratedIn.ShouldBe(5m);
        var receipts = summary.ByCategory.Single(category => category.CategoryName == "Entradas");
        receipts.Count.ShouldBe(2);
        receipts.TotalIn.ShouldBe(35m);
        receipts.GeneratedCount.ShouldBe(0);
        summary.ByCategory.Single(category => category.CategoryName == "Despesas").GeneratedCount.ShouldBe(0);

        otherSummary.TotalTransactionCount.ShouldBe(2);
        otherSummary.GeneratedTransactionCount.ShouldBe(1);
        otherSummary.GeneratedOutValue.ShouldBe(70m);
    }

    private async Task<ResponseCreateTransactionJson> Created<TRequest>(string url, TRequest request, string token)
    {
        var response = await _client.PostAuthAsync(url, request, token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private async Task<ResponseOperatorTransactionSummaryJson> Summary(Guid operatorId, FiadoLinkedMovementTestContext ctx)
    {
        var response = await _client.GetAuthAsync(
            $"/report/operator-summary?operatorId={operatorId}&dateFrom={ctx.Today:yyyy-MM-dd}&dateTo={ctx.Today:yyyy-MM-dd}",
            ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseOperatorTransactionSummaryJson>();
    }
}
