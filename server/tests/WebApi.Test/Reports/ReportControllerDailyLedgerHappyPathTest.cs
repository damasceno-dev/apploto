using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerDailyLedgerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DailyLedger_ShouldReturn200WithCorrectBalancesAndTotals_WhenManagerRequests()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy1");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa Principal");
        var category = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, "Depósito");
        var op = await factory.SeedOperatorAsync(branch.Id, "Op1");
        var client = await factory.SeedClientAsync(branch.Id, "João Silva");
        var dateFrom = new DateTime(2025, 6, 1);
        var dateTo = new DateTime(2025, 6, 30);
        var dueDate = new DateTime(2025, 6, 15);
        var paidAt = new DateTime(2025, 6, 2);

        // Transaction before window — contributes to opening balance
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: new DateTime(2025, 5, 15), value: 500m);

        // Transactions inside window
        var tx = await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: dateFrom, value: 200m,
            description: "Depósito inicial",
            clientId: client.Id,
            dueDate: dueDate,
            paidAt: paidAt);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id,
            date: dateFrom.AddDays(1), value: 80m);

        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-06-01&dateTo=2025-06-30&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyLedgerJson>();
        payload.AccountId.ShouldBe(account.Id);
        payload.AccountName.ShouldBe("Caixa Principal");
        payload.Items.Count.ShouldBe(2);
        payload.TotalCount.ShouldBe(2);
        payload.OpeningBalance.ShouldBe(500m);
        payload.TotalIn.ShouldBe(200m);
        payload.TotalOut.ShouldBe(80m);
        payload.NetTotal.ShouldBe(120m);
        payload.ClosingBalance.ShouldBe(620m);

        // Verify item projection fields — ordered Date ASC, so index 0 is the In row on dateFrom
        var firstItem = payload.Items[0];
        firstItem.Id.ShouldBe(tx.Id);
        firstItem.Date.Date.ShouldBe(dateFrom.Date);
        firstItem.Direction.ShouldBe(Direction.In);
        firstItem.Value.ShouldBe(200m);
        firstItem.Description.ShouldBe("Depósito inicial");
        firstItem.TransactionTypeName.ShouldBe("Depósito");
        firstItem.CategoryName.ShouldBe("Entradas");
        firstItem.RecordedByOperatorName.ShouldBe("Op1");
        firstItem.ClientName.ShouldBe("João Silva");
        firstItem.DueDate.Date.ShouldBe(dueDate.Date);
        firstItem.PaidAt.ShouldNotBeNull();
        firstItem.PaidAt!.Value.Date.ShouldBe(paidAt.Date);
    }

    [Fact]
    public async Task DailyLedger_ShouldReturn200_WhenAdminRequests()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy2", Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 100m);

        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-06-01&dateTo=2025-06-30&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyLedgerJson>();
        payload.Items.Count.ShouldBe(1);
        payload.TotalIn.ShouldBe(100m);
    }

    [Fact]
    public async Task DailyLedger_ShouldReturnEmptyItemsWithZeroTotals_WhenWindowHasNoTransactions()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy3");
        var account = await factory.SeedAccountAsync(branch.Id);

        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-06-01&dateTo=2025-06-30&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyLedgerJson>();
        payload.Items.ShouldBeEmpty();
        payload.TotalCount.ShouldBe(0);
        payload.TotalIn.ShouldBe(0m);
        payload.TotalOut.ShouldBe(0m);
        payload.NetTotal.ShouldBe(0m);
        payload.OpeningBalance.ShouldBe(0m);
        payload.ClosingBalance.ShouldBe(0m);
    }

    [Fact]
    public async Task DailyLedger_ShouldPaginateCorrectly_WhenMultiplePagesExist()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy4");
        var account = await factory.SeedAccountAsync(branch.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var baseDate = new DateTime(2025, 7, 1);

        // Seed 5 transactions on consecutive dates for deterministic ordering
        for (var i = 0; i < 5; i++)
        {
            await factory.SeedTransactionAsync(
                branch.Id, account.Id, transactionType.Id, category.Id,
                Direction.In, op.Id, user.Id,
                date: baseDate.AddDays(i), value: 10m * (i + 1));
        }

        var page1Url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-07-01&dateTo=2025-07-31&page=1&pageSize=2";
        var page2Url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-07-01&dateTo=2025-07-31&page=2&pageSize=2";
        var page3Url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-07-01&dateTo=2025-07-31&page=3&pageSize=2";

        var page1Response = await _client.GetAuthAsync(page1Url, token);
        var page2Response = await _client.GetAuthAsync(page2Url, token);
        var page3Response = await _client.GetAuthAsync(page3Url, token);

        page1Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page2Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        page3Response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var p1 = await page1Response.ReadContentAsync<ResponseDailyLedgerJson>();
        var p2 = await page2Response.ReadContentAsync<ResponseDailyLedgerJson>();
        var p3 = await page3Response.ReadContentAsync<ResponseDailyLedgerJson>();

        p1.TotalCount.ShouldBe(5);
        p1.TotalPages.ShouldBe(3);
        p1.HasNext.ShouldBeTrue();
        p1.HasPrevious.ShouldBeFalse();
        p1.Items.Count.ShouldBe(2);

        p2.HasNext.ShouldBeTrue();
        p2.HasPrevious.ShouldBeTrue();
        p2.Items.Count.ShouldBe(2);

        p3.HasNext.ShouldBeFalse();
        p3.HasPrevious.ShouldBeTrue();
        p3.Items.Count.ShouldBe(1);

        // Items ordered Date ASC — p1 items should have earlier dates than p2
        p1.Items[0].Date.ShouldBeLessThanOrEqualTo(p1.Items[1].Date);
        p1.Items[1].Date.ShouldBeLessThanOrEqualTo(p2.Items[0].Date);
    }

    [Fact]
    public async Task DailyLedger_ShouldExcludeDraftAndCancelledTransactions_WhenWindowContainsThem()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy5");
        var account = await factory.SeedAccountAsync(branch.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var dateIn = new DateTime(2025, 8, 1);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: dateIn, value: 100m, status: TransactionStatus.Active);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: dateIn, value: 999m, status: TransactionStatus.Draft);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id,
            date: dateIn, value: 999m, status: TransactionStatus.Cancelled);

        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-08-01&dateTo=2025-08-31&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyLedgerJson>();
        payload.Items.Count.ShouldBe(1);
        payload.TotalIn.ShouldBe(100m);
    }

    [Fact]
    public async Task DailyLedger_ShouldReduceOpeningBalance_WhenPriorWindowContainsOutRows()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DLHappy6");
        var account = await factory.SeedAccountAsync(branch.Id);
        var category = await factory.SeedCategoryAsync(branch.Id);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var priorDate = new DateTime(2025, 9, 15);
        var dateFrom = new DateTime(2025, 10, 1);
        var dateTo = new DateTime(2025, 10, 31);

        // Prior-window: +600 In, −150 Out → opening balance = 450
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id, date: priorDate, value: 600m);
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.Out, op.Id, user.Id, date: priorDate, value: 150m);

        // Prior-window draft and cancelled — must not contribute to opening balance
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id, date: priorDate, value: 999m,
            status: TransactionStatus.Draft);

        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id, date: priorDate, value: 999m,
            status: TransactionStatus.Cancelled);

        // Window transaction
        await factory.SeedTransactionAsync(
            branch.Id, account.Id, transactionType.Id, category.Id,
            Direction.In, op.Id, user.Id, date: dateFrom, value: 50m);

        var url = $"/report/daily-ledger?accountId={account.Id}&dateFrom=2025-10-01&dateTo=2025-10-31&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyLedgerJson>();
        payload.OpeningBalance.ShouldBe(450m);   // 600 In − 150 Out (draft excluded)
        payload.TotalIn.ShouldBe(50m);
        payload.NetTotal.ShouldBe(50m);
        payload.ClosingBalance.ShouldBe(500m);
    }
}
