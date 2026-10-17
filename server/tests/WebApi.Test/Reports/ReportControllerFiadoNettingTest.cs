using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M7.7 Phase 5.1 read side (decision 1.3, FIFO query-time netting): balance and aging read one branch/client-wide
/// projection. Every In of a client, on any Tab, settles that client's Out rows oldest-first by
/// <c>DueDate, Date, Id</c>; aging shows only the unpaid remainders, an account filter narrows the netted result,
/// and repayments or credit never age as debt.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerFiadoNettingTest(ServerWebApplicationFactory factory)
{
    private static readonly DateTime AsOf = new(2025, 6, 30);

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Netting_ShouldSettleTheOldestDebtAcrossTabs_WhenARepaymentLandsOnAnotherTab()
    {
        var ctx = await SeedAsync("FiadoNetMultiTab");
        var client = await factory.SeedClientAsync(ctx.BranchId, "Carla");
        var oldOnA = await ctx.SaleAsync(ctx.TabA, client.Id, 100m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 10));
        var newOnB = await ctx.SaleAsync(ctx.TabB, client.Id, 80m, new DateTime(2025, 6, 1), new DateTime(2025, 6, 10));
        await ctx.RepaymentAsync(ctx.TabB, client.Id, 100m, new DateTime(2025, 6, 15));

        var all = await AgingAsync(ctx.Token, $"clientId={client.Id}");
        var onA = await AgingAsync(ctx.Token, $"clientId={client.Id}&accountId={ctx.TabA}");
        var onB = await AgingAsync(ctx.Token, $"clientId={client.Id}&accountId={ctx.TabB}");
        var balance = await BalanceAsync(ctx.Token, $"clientId={client.Id}");

        // The repayment on Tab B settles Tab A's older sale first; filtering by account never re-nets per Tab.
        all.Items.ShouldHaveSingleItem().TransactionId.ShouldBe(newOnB);
        all.Items[0].Value.ShouldBe(80m);
        onA.Items.ShouldBeEmpty();
        onA.TotalCount.ShouldBe(0);
        onB.Items.ShouldHaveSingleItem().TransactionId.ShouldBe(newOnB);
        balance.Items.ShouldHaveSingleItem().OutstandingTotal.ShouldBe(80m);
        oldOnA.ShouldNotBe(newOnB);
    }

    [Fact]
    public async Task Netting_ShouldShowOnlyRemainders_WhenAPartialPaymentSpansSales()
    {
        var ctx = await SeedAsync("FiadoNetPartial");
        var client = await factory.SeedClientAsync(ctx.BranchId);
        var first = await ctx.SaleAsync(ctx.TabA, client.Id, 50m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 5));
        var second = await ctx.SaleAsync(ctx.TabA, client.Id, 70m, new DateTime(2025, 5, 2), new DateTime(2025, 5, 20));
        var third = await ctx.SaleAsync(ctx.TabA, client.Id, 30m, new DateTime(2025, 5, 3), new DateTime(2025, 6, 1));
        await ctx.RepaymentAsync(ctx.TabA, client.Id, 60m, new DateTime(2025, 6, 2));
        await ctx.RepaymentAsync(ctx.TabB, client.Id, 25m, new DateTime(2025, 6, 3));

        var aging = await AgingAsync(ctx.Token, $"clientId={client.Id}");
        var balance = await BalanceAsync(ctx.Token, $"clientId={client.Id}");

        // 85 In: settles 50 fully, then 35 of 70; the 30 sale is untouched.
        aging.Items.Select(item => (item.TransactionId, item.Value)).ShouldBe([(second, 35m), (third, 30m)]);
        aging.Items.Select(item => item.TransactionId).ShouldNotContain(first);
        aging.Items.Sum(item => item.Value).ShouldBe(65m);
        balance.Items.ShouldHaveSingleItem().OutstandingTotal.ShouldBe(65m);
    }

    [Fact]
    public async Task Netting_ShouldLeaveNoAging_WhenFullySettledOrOverpaid()
    {
        var ctx = await SeedAsync("FiadoNetFullAndCredit");
        var settled = await factory.SeedClientAsync(ctx.BranchId, "Quitado");
        var credit = await factory.SeedClientAsync(ctx.BranchId, "Credito");
        await ctx.SaleAsync(ctx.TabA, settled.Id, 40m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 1));
        await ctx.RepaymentAsync(ctx.TabB, settled.Id, 40m, new DateTime(2025, 5, 2));
        await ctx.SaleAsync(ctx.TabA, credit.Id, 40m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 1));
        await ctx.RepaymentAsync(ctx.TabA, credit.Id, 55m, new DateTime(2025, 5, 2));

        var settledAging = await AgingAsync(ctx.Token, $"clientId={settled.Id}");
        var creditAging = await AgingAsync(ctx.Token, $"clientId={credit.Id}");
        var balances = await BalanceAsync(ctx.Token, string.Empty);

        settledAging.Items.ShouldBeEmpty();
        creditAging.Items.ShouldBeEmpty();
        creditAging.TotalCount.ShouldBe(0);
        // A settled client drops out of the balance list; a client in credit keeps the signed negative balance.
        balances.Items.ShouldHaveSingleItem().ClientId.ShouldBe(credit.Id);
        balances.Items[0].OutstandingTotal.ShouldBe(-15m);
        balances.TotalOutstanding.ShouldBe(-15m);
    }

    [Fact]
    public async Task Netting_ShouldNeverAgeRepaymentsOrCredit_AndReturnEmptyWithNoRows()
    {
        var ctx = await SeedAsync("FiadoNetNoDebt");
        var prepaid = await factory.SeedClientAsync(ctx.BranchId, "Adiantado");
        var repayment = await ctx.RepaymentAsync(ctx.TabA, prepaid.Id, 30m, new DateTime(2025, 6, 1));

        var aging = await AgingAsync(ctx.Token, string.Empty);
        var balance = await BalanceAsync(ctx.Token, string.Empty);
        var emptyCtx = await SeedAsync("FiadoNetEmpty");
        var emptyAging = await AgingAsync(emptyCtx.Token, string.Empty);
        var emptyBalance = await BalanceAsync(emptyCtx.Token, string.Empty);

        aging.Items.Select(item => item.TransactionId).ShouldNotContain(repayment);
        aging.TotalCount.ShouldBe(0);
        balance.Items.ShouldHaveSingleItem().OutstandingTotal.ShouldBe(-30m);
        emptyAging.Items.ShouldBeEmpty();
        emptyAging.TotalCount.ShouldBe(0);
        emptyBalance.Items.ShouldBeEmpty();
        emptyBalance.TotalOutstanding.ShouldBe(0m);
    }

    [Fact]
    public async Task Netting_ShouldUseTheSameAsOfBoundary_ForBalanceAndAging()
    {
        var ctx = await SeedAsync("FiadoNetAsOf");
        var client = await factory.SeedClientAsync(ctx.BranchId);
        var onAsOf = await ctx.SaleAsync(ctx.TabA, client.Id, 20m, AsOf, AsOf.AddDays(30));
        var dueSooner = await ctx.SaleAsync(ctx.TabA, client.Id, 500m, AsOf.AddDays(1), AsOf.AddDays(1));
        await ctx.RepaymentAsync(ctx.TabA, client.Id, 20m, AsOf.AddDays(1));

        var aging = await AgingAsync(ctx.Token, $"clientId={client.Id}");
        var balance = await BalanceAsync(ctx.Token, $"clientId={client.Id}");
        var nextDayAging = await AgingAsync(ctx.Token, $"clientId={client.Id}", AsOf.AddDays(1));

        // Rows dated after AsOfDate are outside both reads; a row due later is still debt in the Current bucket.
        var item = aging.Items.ShouldHaveSingleItem();
        item.TransactionId.ShouldBe(onAsOf);
        item.Bucket.ShouldBe(AgingBucket.Current);
        balance.Items.ShouldHaveSingleItem().OutstandingTotal.ShouldBe(20m);
        // A day later FIFO runs by due date: the 20 repayment settles part of the later-dated sale due sooner
        // (AsOf+1), not the earlier-dated sale due AsOf+30.
        nextDayAging.Items.Select(row => (row.TransactionId, row.Value)).ShouldBe([(dueSooner, 480m), (onAsOf, 20m)]);
        nextDayAging.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task Netting_ShouldBreakTiesById_WhenDueDateAndDateMatch()
    {
        var ctx = await SeedAsync("FiadoNetTies");
        var client = await factory.SeedClientAsync(ctx.BranchId);
        var lowId = Guid.Parse("00000000-0000-0000-0000-00000000a001");
        var highId = Guid.Parse("00000000-0000-0000-0000-00000000a002");
        var due = new DateTime(2025, 5, 10);
        await ctx.SaleAsync(ctx.TabA, client.Id, 10m, new DateTime(2025, 5, 1), due, highId);
        await ctx.SaleAsync(ctx.TabB, client.Id, 10m, new DateTime(2025, 5, 1), due, lowId);
        await ctx.RepaymentAsync(ctx.TabA, client.Id, 15m, new DateTime(2025, 5, 2));

        var aging = await AgingAsync(ctx.Token, $"clientId={client.Id}");

        aging.Items.Select(item => (item.TransactionId, item.Value)).ShouldBe([(highId, 5m)]);
    }

    [Fact]
    public async Task Netting_ShouldMatchTheBalance_AcrossEveryAgingPage()
    {
        var ctx = await SeedAsync("FiadoNetPages");
        var clientA = await factory.SeedClientAsync(ctx.BranchId, "A");
        var clientB = await factory.SeedClientAsync(ctx.BranchId, "B");
        foreach (var day in Enumerable.Range(1, 6))
        {
            await ctx.SaleAsync(day % 2 == 0 ? ctx.TabA : ctx.TabB, clientA.Id, 10m * day, new DateTime(2025, 5, day), new DateTime(2025, 5, day));
            await ctx.SaleAsync(ctx.TabA, clientB.Id, 7m, new DateTime(2025, 5, day), new DateTime(2025, 5, day));
        }

        await ctx.RepaymentAsync(ctx.TabB, clientA.Id, 45m, new DateTime(2025, 6, 1));
        await ctx.RepaymentAsync(ctx.TabA, clientB.Id, 10m, new DateTime(2025, 6, 1));
        // Draft and cancelled repayments never settle anything.
        await ctx.RepaymentAsync(ctx.TabA, clientA.Id, 1000m, new DateTime(2025, 6, 2), TransactionStatus.Draft);
        await ctx.RepaymentAsync(ctx.TabA, clientA.Id, 1000m, new DateTime(2025, 6, 2), TransactionStatus.Cancelled);

        var pages = new List<ResponseFiadoAgingItemJson>();
        // A: 210 owed, 45 repaid → 10 and 20 settled, 15 of 30 left, plus 40/50/60 (4 rows).
        // B: 42 owed, 10 repaid → 7 settled, 4 of 7 left, plus four 7s (5 rows).
        for (var page = 1; page <= 3; page++)
        {
            var response = await AgingAsync(ctx.Token, $"pageSize=3&page={page}");
            response.TotalCount.ShouldBe(9);
            response.TotalPages.ShouldBe(3);
            pages.AddRange(response.Items);
        }

        var balances = await BalanceAsync(ctx.Token, string.Empty);

        pages.Select(item => item.TransactionId).Distinct().Count().ShouldBe(9);
        pages.Sum(item => item.Value).ShouldBe(balances.TotalOutstanding);
        pages.Where(item => item.ClientId == clientA.Id).Sum(item => item.Value).ShouldBe(210m - 45m);
        pages.Where(item => item.ClientId == clientB.Id).Sum(item => item.Value).ShouldBe(42m - 10m);
        pages.Select(item => (item.DueDate, item.Date)).ShouldBe(pages.Select(item => (item.DueDate, item.Date)).Order());
    }

    [Fact]
    public async Task Netting_ShouldIsolateBranches_WhenAnotherBranchRepaysTheSameShape()
    {
        var ctx = await SeedAsync("FiadoNetBranchA");
        var other = await SeedAsync("FiadoNetBranchB");
        var client = await factory.SeedClientAsync(ctx.BranchId);
        var otherClient = await factory.SeedClientAsync(other.BranchId);
        var sale = await ctx.SaleAsync(ctx.TabA, client.Id, 90m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 1));
        await other.SaleAsync(other.TabA, otherClient.Id, 90m, new DateTime(2025, 5, 1), new DateTime(2025, 5, 1));
        await other.RepaymentAsync(other.TabA, otherClient.Id, 90m, new DateTime(2025, 5, 2));

        var aging = await AgingAsync(ctx.Token, string.Empty);
        var foreignClient = await AgingAsync(ctx.Token, $"clientId={otherClient.Id}");
        var otherAging = await AgingAsync(other.Token, string.Empty);

        aging.Items.ShouldHaveSingleItem().TransactionId.ShouldBe(sale);
        aging.Items[0].Value.ShouldBe(90m);
        foreignClient.Items.ShouldBeEmpty();
        otherAging.Items.ShouldBeEmpty();
    }

    private async Task<ResponseFiadoAgingJson> AgingAsync(string token, string query, DateTime? asOf = null)
    {
        var separator = query.Length == 0 ? string.Empty : "&";
        var url = $"/report/fiado/aging?asOfDate={asOf ?? AsOf:yyyy-MM-dd}{separator}{query}" +
                  (query.Contains("pageSize") ? string.Empty : "&page=1&pageSize=50");
        var response = await _client.GetAuthAsync(url, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseFiadoAgingJson>();
    }

    private async Task<ResponseFiadoBalanceJson> BalanceAsync(string token, string query)
    {
        var separator = query.Length == 0 ? string.Empty : "&";
        var response = await _client.GetAuthAsync($"/report/fiado/balance?asOfDate={AsOf:yyyy-MM-dd}{separator}{query}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseFiadoBalanceJson>();
    }

    private async Task<NettingContext> SeedAsync(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label);
        var tabA = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado A");
        var tabB = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado B");
        var outCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado saída", Direction.Out);
        var inCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado entrada", Direction.In);
        var saleType = await factory.SeedTransactionTypeAsync(outCategory.Id, "Venda fiado", requiresTabAccountAndClient: true);
        var repaymentType = await factory.SeedTransactionTypeAsync(inCategory.Id, "Recebimento fiado", requiresTabAccountAndClient: true);
        var op = await factory.SeedOperatorAsync(branch.Id);
        return new NettingContext(factory, branch.Id, user.Id, op.Id, tabA.Id, tabB.Id, outCategory.Id, inCategory.Id, saleType.Id, repaymentType.Id, token);
    }

    private sealed record NettingContext(
        ServerWebApplicationFactory Factory,
        Guid BranchId,
        Guid UserId,
        Guid OperatorId,
        Guid TabA,
        Guid TabB,
        Guid OutCategoryId,
        Guid InCategoryId,
        Guid SaleTypeId,
        Guid RepaymentTypeId,
        string Token)
    {
        public async Task<Guid> SaleAsync(Guid tab, Guid clientId, decimal value, DateTime date, DateTime dueDate, Guid? id = null)
        {
            var row = await Factory.SeedTransactionAsync(
                BranchId, tab, SaleTypeId, OutCategoryId, Direction.Out, OperatorId, UserId,
                date: date, value: value, clientId: clientId, dueDate: dueDate, id: id);
            return row.Id;
        }

        public async Task<Guid> RepaymentAsync(
            Guid tab,
            Guid clientId,
            decimal value,
            DateTime date,
            TransactionStatus status = TransactionStatus.Active)
        {
            var row = await Factory.SeedTransactionAsync(
                BranchId, tab, RepaymentTypeId, InCategoryId, Direction.In, OperatorId, UserId,
                date: date, value: value, clientId: clientId, dueDate: date, status: status);
            return row.Id;
        }
    }
}
