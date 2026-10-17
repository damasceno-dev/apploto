using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 3.3: fiado balance debt/credit split and FIFO aging whole-set totals on the M7.7 Phase 5 netting — a
/// multi-Tab debtor, a partial payment, a settled client (absent) and a client in credit (never debt or aging);
/// balance equals the aging remainder per client and across every page.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoAggregatesTest(ServerWebApplicationFactory factory)
{
    private static readonly DateTime AsOf = new(2025, 6, 30);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Aggregates_ShouldAgreeBetweenBalanceAndAging_ForEveryClientKind()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M11FiadoAggregates");
        var tabA = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado A");
        var tabB = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado B");
        var outCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado saída", Direction.Out);
        var inCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado entrada", Direction.In);
        var saleType = await factory.SeedTransactionTypeAsync(outCategory.Id, requiresTabAccountAndClient: true);
        var payType = await factory.SeedTransactionTypeAsync(inCategory.Id, requiresTabAccountAndClient: true);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var debtor = await factory.SeedClientAsync(branch.Id, "Devedor multi-Tab");
        var partial = await factory.SeedClientAsync(branch.Id, "Pagamento parcial");
        var settled = await factory.SeedClientAsync(branch.Id, "Quitado");
        var credit = await factory.SeedClientAsync(branch.Id, "Credito");

        async Task Sale(Guid tab, Guid client, decimal value, DateTime date) =>
            await factory.SeedTransactionAsync(branch.Id, tab, saleType.Id, outCategory.Id, Direction.Out, op.Id, user.Id,
                date: date, value: value, clientId: client, dueDate: date);
        async Task Pay(Guid tab, Guid client, decimal value, DateTime date, TransactionStatus status = TransactionStatus.Active) =>
            await factory.SeedTransactionAsync(branch.Id, tab, payType.Id, inCategory.Id, Direction.In, op.Id, user.Id,
                date: date, value: value, clientId: client, dueDate: date, status: status);

        // Debtor across two Tabs: four sales, one oldest sale partly repaid on the other Tab.
        await Sale(tabA.Id, debtor.Id, 100m, new DateTime(2025, 2, 10));
        await Sale(tabB.Id, debtor.Id, 60m, new DateTime(2025, 4, 15));
        await Sale(tabA.Id, debtor.Id, 40m, new DateTime(2025, 6, 10));
        await Sale(tabB.Id, debtor.Id, 25m, new DateTime(2025, 6, 28));
        await Pay(tabB.Id, debtor.Id, 70m, new DateTime(2025, 6, 1));
        // Partial payment spanning two sales.
        await Sale(tabA.Id, partial.Id, 30m, new DateTime(2025, 5, 1));
        await Sale(tabA.Id, partial.Id, 30m, new DateTime(2025, 5, 20));
        await Pay(tabA.Id, partial.Id, 45m, new DateTime(2025, 6, 2));
        // Settled and in-credit clients; Draft and cancelled repayments never count.
        await Sale(tabA.Id, settled.Id, 80m, new DateTime(2025, 3, 1));
        await Pay(tabB.Id, settled.Id, 80m, new DateTime(2025, 3, 20));
        await Sale(tabB.Id, credit.Id, 20m, new DateTime(2025, 3, 1));
        await Pay(tabA.Id, credit.Id, 35m, new DateTime(2025, 3, 2));
        await Pay(tabA.Id, debtor.Id, 1000m, new DateTime(2025, 6, 5), TransactionStatus.Draft);
        await Pay(tabA.Id, debtor.Id, 1000m, new DateTime(2025, 6, 5), TransactionStatus.Cancelled);

        var balance = await Get<ResponseFiadoBalanceJson>($"/report/fiado/balance?asOfDate={AsOf:yyyy-MM-dd}", token);
        var pages = new List<ResponseFiadoAgingJson>();
        for (var page = 1; page <= 3; page++)
            pages.Add(await Get<ResponseFiadoAgingJson>($"/report/fiado/aging?asOfDate={AsOf:yyyy-MM-dd}&page={page}&pageSize=2", token));
        var rows = pages.SelectMany(page => page.Items).ToList();

        // Balance: debtor 155, partial 15, credit −15; the settled client is absent.
        balance.Items.Select(item => (item.ClientName, item.OutstandingTotal)).OrderBy(x => x.ClientName)
            .ShouldBe(new[] { ("Credito", -15m), ("Devedor multi-Tab", 155m), ("Pagamento parcial", 15m) }.OrderBy(x => x.Item1));
        balance.TotalOutstanding.ShouldBe(155m);
        balance.DebtorCount.ShouldBe(2);
        balance.TotalDebt.ShouldBe(170m);
        balance.CreditorCount.ShouldBe(1);
        balance.TotalCredit.ShouldBe(15m);

        // Aging across every page: only unpaid sale remainders of debtors; credit and repayments never age.
        pages.Select(page => page.Items.Count).ShouldBe([2, 2, 1]);
        rows.Count.ShouldBe(5);
        rows.ShouldNotContain(row => row.ClientId == credit.Id || row.ClientId == settled.Id);
        foreach (var debtorItem in balance.Items.Where(item => item.OutstandingTotal > 0m))
            rows.Where(row => row.ClientId == debtorItem.ClientId).Sum(row => row.Value).ShouldBe(debtorItem.OutstandingTotal);
        pages.ShouldAllBe(page => page.OutstandingTotal == 170m && page.TotalCount == 5);
        pages[0].OutstandingTotal.ShouldBe(balance.TotalDebt);
        pages[0].ByBucket.Sum(bucket => bucket.Value).ShouldBe(170m);
        pages[0].ByBucket.Sum(bucket => bucket.RowCount).ShouldBe(5);
        foreach (var bucket in pages[0].ByBucket)
            bucket.Value.ShouldBe(rows.Where(row => row.Bucket == bucket.Bucket).Sum(row => row.Value));
        // Debtor: 70 settles the Feb sale (100 → 30 left); partial: 45 settles the first 30 and 15 of the second.
        // The oldest unpaid SALE date across the set is the debtor's February sale, still partly open.
        pages.ShouldAllBe(page => page.OldestUnpaidDate == new DateTime(2025, 2, 10));
    }

    [Fact]
    public async Task Aggregates_ShouldFollowTheFilters_AppliedAfterNetting()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M11FiadoAggregatesFilter");
        var tabA = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado A");
        var tabB = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado B");
        var outCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado saída", Direction.Out);
        var inCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado entrada", Direction.In);
        var saleType = await factory.SeedTransactionTypeAsync(outCategory.Id, requiresTabAccountAndClient: true);
        var payType = await factory.SeedTransactionTypeAsync(inCategory.Id, requiresTabAccountAndClient: true);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var client = await factory.SeedClientAsync(branch.Id);
        await factory.SeedTransactionAsync(branch.Id, tabA.Id, saleType.Id, outCategory.Id, Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 5, 1), value: 100m, clientId: client.Id, dueDate: new DateTime(2025, 5, 1));
        await factory.SeedTransactionAsync(branch.Id, tabB.Id, saleType.Id, outCategory.Id, Direction.Out, op.Id, user.Id,
            date: new DateTime(2025, 6, 1), value: 50m, clientId: client.Id, dueDate: new DateTime(2025, 6, 1));
        await factory.SeedTransactionAsync(branch.Id, tabB.Id, payType.Id, inCategory.Id, Direction.In, op.Id, user.Id,
            date: new DateTime(2025, 6, 2), value: 100m, clientId: client.Id, dueDate: new DateTime(2025, 6, 2));

        var onA = await Get<ResponseFiadoAgingJson>($"/report/fiado/aging?accountId={tabA.Id}&asOfDate={AsOf:yyyy-MM-dd}&page=1&pageSize=10", token);
        var onB = await Get<ResponseFiadoAgingJson>($"/report/fiado/aging?accountId={tabB.Id}&asOfDate={AsOf:yyyy-MM-dd}&page=1&pageSize=10", token);

        // The repayment on Tab B settles Tab A's older sale first; totals follow the netted, then filtered, set.
        onA.OutstandingTotal.ShouldBe(0m);
        onA.OldestUnpaidDate.ShouldBeNull();
        onA.ByBucket.ShouldAllBe(bucket => bucket.Value == 0m && bucket.RowCount == 0);
        onB.OutstandingTotal.ShouldBe(50m);
        onB.OldestUnpaidDate.ShouldBe(new DateTime(2025, 6, 1));
    }

    private async Task<T> Get<T>(string url, string token)
        where T : class
    {
        var response = await _client.GetAuthAsync(url, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<T>();
    }
}
