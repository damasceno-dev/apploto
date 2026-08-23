using CommonTestUtilities.Requests;
using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerCashVarianceHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Seeded "Diferença Caixa" items across multiple days and accounts
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturnAllRows_WhenMultipleDaysAndAccountsSeeded()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy1", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta A");
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta B");

        var day1 = new DateTime(2025, 3, 1);
        var day2 = new DateTime(2025, 3, 2);
        var day3 = new DateTime(2025, 3, 3);

        // Account A: 2 closes on different days
        var closeA1 = await factory.SeedDailyCloseAsync(branch.Id, accountA.Id, date: day1, status: DailyCloseStatus.Approved);
        var closeA2 = await factory.SeedDailyCloseAsync(branch.Id, accountA.Id, date: day2, status: DailyCloseStatus.Submitted);

        // Account B: 1 close
        var closeB = await factory.SeedDailyCloseAsync(branch.Id, accountB.Id, date: day3, status: DailyCloseStatus.Draft);

        await factory.SeedDailyCloseItemAsync(closeA1.Id, product.Id, value: 150m);
        await factory.SeedDailyCloseItemAsync(closeA2.Id, product.Id, value: -30m);
        await factory.SeedDailyCloseItemAsync(closeB.Id, product.Id, value: 200m);

        const string url = "/report/cash-variance?dateFrom=2025-03-01&dateTo=2025-03-31&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();

        payload.TotalCount.ShouldBe(2);
        payload.Items.Count.ShouldBe(2);
        payload.TotalVariance.ShouldBe(120m);
        payload.MeanVariance.ShouldBe(60m);
        payload.MaxVariance.ShouldBe(150m);
        payload.MinVariance.ShouldBe(-30m);
        payload.TotalPages.ShouldBe(1);
        payload.HasNext.ShouldBeFalse();
        payload.HasPrevious.ShouldBeFalse();

        payload.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Approved).ShouldBeTrue();
        payload.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Submitted).ShouldBeTrue();
        payload.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Draft).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Reload-based assertion: item values match persisted DailyCloseItems
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturnPersistedVarianceValues_WhenReloaded()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy2", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta Reload");

        var date = new DateTime(2025, 4, 15);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: date, status: DailyCloseStatus.Approved);
        var seededItem = await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 333m);

        var reloadedItem = await factory.ReloadAsync<server.Domain.Entities.DailyCloseItem>(seededItem.Id);
        reloadedItem.ShouldNotBeNull();
        reloadedItem.Value.ShouldBe(333m);

        var url = $"/report/cash-variance?dateFrom=2025-04-01&dateTo=2025-04-30&accountId={account.Id}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();

        payload.TotalCount.ShouldBe(1);
        payload.Items[0].VarianceValue.ShouldBe(reloadedItem.Value);
        payload.Items[0].AccountId.ShouldBe(account.Id);
    }

    // -------------------------------------------------------------------------
    // Account filter narrows results
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturnOnlyMatchingAccount_WhenAccountIdProvided()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy3", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta Filter A");
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Conta Filter B");

        var day = new DateTime(2025, 5, 10);
        var closeA = await factory.SeedDailyCloseAsync(branch.Id, accountA.Id, date: day, status: DailyCloseStatus.Approved);
        var closeB = await factory.SeedDailyCloseAsync(branch.Id, accountB.Id, date: day, status: DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(closeA.Id, product.Id, value: 100m);
        await factory.SeedDailyCloseItemAsync(closeB.Id, product.Id, value: 999m);

        var url = $"/report/cash-variance?dateFrom=2025-05-01&dateTo=2025-05-31&accountId={accountA.Id}&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();

        payload.TotalCount.ShouldBe(1);
        payload.Items[0].DailyCloseId.ShouldBe(closeA.Id);
        payload.Items[0].AccountId.ShouldBe(accountA.Id);
        payload.Items[0].VarianceValue.ShouldBe(100m);

        var allAccountsResponse = await _client.GetAuthAsync(
            "/report/cash-variance?dateFrom=2025-05-01&dateTo=2025-05-31&page=1&pageSize=50",
            token);
        var allAccounts = await allAccountsResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();
        allAccounts.Items.Single(item => item.AccountId == accountA.Id).DailyCloseId.ShouldBe(closeA.Id);
        allAccounts.Items.Single(item => item.AccountId == accountB.Id).DailyCloseId.ShouldBe(closeB.Id);
    }

    // -------------------------------------------------------------------------
    // Empty window → zero aggregates and empty items
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturnZeroAggregatesAndEmptyItems_WhenNoClosesInWindow()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy4", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        const string url = "/report/cash-variance?dateFrom=2020-01-01&dateTo=2020-01-31&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();

        payload.TotalCount.ShouldBe(0);
        payload.Items.ShouldBeEmpty();
        payload.TotalVariance.ShouldBe(0m);
        payload.MeanVariance.ShouldBe(0m);
        payload.MaxVariance.ShouldBe(0m);
        payload.MinVariance.ShouldBe(0m);
    }

    // -------------------------------------------------------------------------
    // Per-branch isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldExcludeOtherBranchRows_WhenAnotherBranchHasActivity()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy5", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("CVHappy5-other");

        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var otherProduct = await factory.SeedProductAsync(otherBranch.Id, "Diferença Caixa");

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);

        var day = new DateTime(2025, 6, 1);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: day, status: DailyCloseStatus.Approved);
        var otherClose = await factory.SeedDailyCloseAsync(otherBranch.Id, otherAccount.Id, date: day, status: DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 100m);
        await factory.SeedDailyCloseItemAsync(otherClose.Id, otherProduct.Id, value: 999m);

        const string url = "/report/cash-variance?dateFrom=2025-06-01&dateTo=2025-06-30&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();

        payload.TotalCount.ShouldBe(1);
        payload.Items[0].VarianceValue.ShouldBe(100m);
    }

    // -------------------------------------------------------------------------
    // Pagination
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturnCorrectPagination_WhenMultipleRowsExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy6", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        for (var i = 1; i <= 5; i++)
        {
            var close = await factory.SeedDailyCloseAsync(
                branch.Id, account.Id,
                date: new DateTime(2025, 7, i),
                status: DailyCloseStatus.Approved);
            await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: i * 10m);
        }

        const string url1 = "/report/cash-variance?dateFrom=2025-07-01&dateTo=2025-07-31&page=1&pageSize=3";
        var r1 = await _client.GetAuthAsync(url1, token);
        var p1 = await r1.ReadContentAsync<ResponseCashVarianceSummaryJson>();
        p1.TotalCount.ShouldBe(5);
        p1.TotalPages.ShouldBe(2);
        p1.Items.Count.ShouldBe(3);
        p1.HasPrevious.ShouldBeFalse();
        p1.HasNext.ShouldBeTrue();

        const string url2 = "/report/cash-variance?dateFrom=2025-07-01&dateTo=2025-07-31&page=2&pageSize=3";
        var r2 = await _client.GetAuthAsync(url2, token);
        var p2 = await r2.ReadContentAsync<ResponseCashVarianceSummaryJson>();
        p2.Items.Count.ShouldBe(2);
        p2.HasPrevious.ShouldBeTrue();
        p2.HasNext.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Admin can access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CashVariance_ShouldReturn200_WhenAdminRequests()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CVHappy7", Role.Admin);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        const string url = "/report/cash-variance?dateFrom=2025-01-01&dateTo=2025-01-31&page=1&pageSize=50";
        var httpResponse = await _client.GetAuthAsync(url, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
