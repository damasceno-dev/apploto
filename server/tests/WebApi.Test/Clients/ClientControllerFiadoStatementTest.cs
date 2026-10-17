using System.Net;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Clients;

/// <summary>
/// M7.7 Phase 5.5: <c>GET /client/{clientId}/fiado-statement</c> — branch-wide across every Tab, full-history running
/// balances, window boundary balances over every page, the shared FIFO position, and the explicit-Tab-grant rule.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ClientControllerFiadoStatementTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly DateTime _today = new BranchClock().LocalBusinessDate(DateTime.UtcNow);

    [Fact]
    public async Task Statement_ShouldSpanEveryTab_AndCloseAtTheBalance_WhenTheWindowIsOmitted()
    {
        var ctx = await SeedAsync("FiadoStmtFull", Role.Manager);
        var client = await factory.SeedClientAsync(ctx.BranchId, "Dona Rosa");
        var sale1 = await ctx.SaleAsync(ctx.TabA, client.Id, 100m, _today.AddDays(-20), _today.AddDays(-10));
        var sale2 = await ctx.SaleAsync(ctx.TabB, client.Id, 60m, _today.AddDays(-15), _today.AddDays(-5));
        var repayment = await ctx.RepaymentAsync(ctx.TabB, client.Id, 120m, _today.AddDays(-3));
        await ctx.RepaymentAsync(ctx.TabA, client.Id, 999m, _today.AddDays(-2), TransactionStatus.Draft);
        await ctx.SaleAsync(ctx.TabA, client.Id, 999m, _today.AddDays(-2), _today.AddDays(-2), status: TransactionStatus.Cancelled);

        var statement = await StatementAsync(client.Id, ctx.Token, string.Empty);
        var balance = await _client.GetAuthAsync($"/report/fiado/balance?clientId={client.Id}", ctx.Token);

        statement.ClientName.ShouldBe("Dona Rosa");
        statement.AsOfDate.Date.ShouldBe(_today);
        statement.DateFrom.ShouldBeNull();
        statement.DateTo.Date.ShouldBe(_today);
        statement.Items.Select(item => item.TransactionId).ShouldBe([repayment, sale2, sale1]);
        statement.Items.Select(item => item.RunningBalance).ShouldBe([40m, 160m, 100m]);
        statement.Items[0].Direction.ShouldBe(Direction.In);
        statement.Items[0].AccountId.ShouldBe(ctx.TabB);
        statement.Items[0].TransactionTypeName.ShouldBe("Recebimento fiado");
        statement.OpeningBalance.ShouldBe(0m);
        statement.ClosingBalance.ShouldBe(40m);
        statement.OutstandingTotal.ShouldBe(40m);
        // 120 In settles sale1 (due first) fully and 20 of sale2: the oldest unpaid SALE date is sale2's.
        statement.OldestUnpaidDate!.Value.Date.ShouldBe(_today.AddDays(-15));
        statement.TotalCount.ShouldBe(3);
        (await balance.ReadContentAsync<ResponseFiadoBalanceJson>()).Items.ShouldHaveSingleItem()
            .OutstandingTotal.ShouldBe(statement.OutstandingTotal);
    }

    [Fact]
    public async Task Statement_ShouldReconcileTheWindowAcrossEveryPage_IncludingTiedDates()
    {
        var ctx = await SeedAsync("FiadoStmtWindow", Role.Admin);
        var client = await factory.SeedClientAsync(ctx.BranchId);
        await ctx.SaleAsync(ctx.TabA, client.Id, 50m, _today.AddDays(-40), _today.AddDays(-40));
        var tiedDay = _today.AddDays(-10);
        var created = DateTime.UtcNow.AddHours(-1);
        for (var index = 0; index < 4; index++)
        {
            await ctx.SaleAsync(ctx.TabA, client.Id, 10m + index, tiedDay, tiedDay, createdAt: created.AddMinutes(index));
        }

        await ctx.RepaymentAsync(ctx.TabB, client.Id, 30m, tiedDay, createdAt: created.AddMinutes(10));
        await ctx.SaleAsync(ctx.TabB, client.Id, 7m, _today.AddDays(-5), _today.AddDays(-5));
        await ctx.SaleAsync(ctx.TabA, client.Id, 1000m, _today.AddDays(-1), _today.AddDays(-1));
        var query = $"dateFrom={_today.AddDays(-30):yyyy-MM-dd}&dateTo={_today.AddDays(-2):yyyy-MM-dd}&pageSize=2";

        var pages = new List<ResponseClientFiadoStatementJson>();
        for (var page = 1; page <= 3; page++)
        {
            pages.Add(await StatementAsync(client.Id, ctx.Token, $"{query}&page={page}"));
        }

        var rows = pages.SelectMany(page => page.Items).ToList();
        pages.ShouldAllBe(page => page.TotalCount == 6 && page.TotalPages == 3 && page.OpeningBalance == 50m);
        pages.ShouldAllBe(page => page.ClosingBalance == 50m + 10m + 11m + 12m + 13m - 30m + 7m);
        pages[2].Items.Count.ShouldBe(2);
        pages[2].HasNext.ShouldBeFalse();
        pages[0].HasPrevious.ShouldBeFalse();

        // Newest first; walking back chronologically, each row's running balance is the previous one plus Out − In.
        var chronological = rows.AsEnumerable().Reverse().ToList();
        var running = pages[0].OpeningBalance;
        foreach (var row in chronological)
        {
            running += row.Direction == Direction.Out ? row.Value : -row.Value;
            row.RunningBalance.ShouldBe(running);
        }

        running.ShouldBe(pages[0].ClosingBalance);
        rows.Select(row => row.Value).Take(2).ShouldBe([7m, 30m]);
        chronological.Take(4).Select(row => row.Value).ShouldBe([10m, 11m, 12m, 13m]);
        // The window ends before today, so its closing is not the current outstanding total.
        pages[0].OutstandingTotal.ShouldBe(pages[0].ClosingBalance + 1000m);
    }

    [Fact]
    public async Task Statement_ShouldReportTheOldestUnpaidSaleDate_NotTheFirstRowInDueOrder()
    {
        var ctx = await SeedAsync("FiadoStmtOldest", Role.Manager);
        var client = await factory.SeedClientAsync(ctx.BranchId);
        // Older sale, due late; newer sale, due first. A payment equal to the newer sale settles it (FIFO by due),
        // so the remaining debt is the OLDER sale.
        await ctx.SaleAsync(ctx.TabA, client.Id, 80m, _today.AddDays(-30), _today.AddDays(30));
        await ctx.SaleAsync(ctx.TabA, client.Id, 25m, _today.AddDays(-10), _today.AddDays(-5));
        await ctx.RepaymentAsync(ctx.TabB, client.Id, 25m, _today.AddDays(-1));

        var statement = await StatementAsync(client.Id, ctx.Token, string.Empty);

        statement.OutstandingTotal.ShouldBe(80m);
        statement.OldestUnpaidDate!.Value.Date.ShouldBe(_today.AddDays(-30));
    }

    [Fact]
    public async Task Statement_ShouldHandleSettledCreditAndEmptyClients()
    {
        var ctx = await SeedAsync("FiadoStmtStates", Role.Manager);
        var settled = await factory.SeedClientAsync(ctx.BranchId, "Quitado");
        var credit = await factory.SeedClientAsync(ctx.BranchId, "Credito");
        var empty = await factory.SeedClientAsync(ctx.BranchId, "Sem fiado");
        await ctx.SaleAsync(ctx.TabA, settled.Id, 30m, _today.AddDays(-5), _today.AddDays(-5));
        await ctx.RepaymentAsync(ctx.TabA, settled.Id, 30m, _today.AddDays(-4));
        await ctx.SaleAsync(ctx.TabA, credit.Id, 30m, _today.AddDays(-5), _today.AddDays(-5));
        await ctx.RepaymentAsync(ctx.TabB, credit.Id, 45m, _today.AddDays(-4));

        var settledStatement = await StatementAsync(settled.Id, ctx.Token, string.Empty);
        var creditStatement = await StatementAsync(credit.Id, ctx.Token, string.Empty);
        var emptyStatement = await StatementAsync(empty.Id, ctx.Token, string.Empty);

        settledStatement.OutstandingTotal.ShouldBe(0m);
        settledStatement.OldestUnpaidDate.ShouldBeNull();
        settledStatement.ClosingBalance.ShouldBe(0m);
        // Credit stays signed in the contract; clients present it as a positive credit.
        creditStatement.OutstandingTotal.ShouldBe(-15m);
        creditStatement.ClosingBalance.ShouldBe(-15m);
        creditStatement.Items[0].RunningBalance.ShouldBe(-15m);
        creditStatement.OldestUnpaidDate.ShouldBeNull();
        emptyStatement.Items.ShouldBeEmpty();
        emptyStatement.TotalCount.ShouldBe(0);
        emptyStatement.TotalPages.ShouldBe(0);
        emptyStatement.OpeningBalance.ShouldBe(0m);
        emptyStatement.ClosingBalance.ShouldBe(0m);
        emptyStatement.OutstandingTotal.ShouldBe(0m);
    }

    [Fact]
    public async Task Statement_ShouldLetAMemberWithATabGrantReadTheWholeBranchHistory()
    {
        var ctx = await SeedAsync("FiadoStmtMemberTab", Role.Member, grantTabA: true);
        var client = await factory.SeedClientAsync(ctx.BranchId);
        await ctx.SaleAsync(ctx.TabA, client.Id, 50m, _today.AddDays(-3), _today.AddDays(-3));
        var repaymentElsewhere = await ctx.RepaymentAsync(ctx.TabB, client.Id, 20m, _today.AddDays(-1));

        var statement = await StatementAsync(client.Id, ctx.Token, string.Empty);

        statement.Items.Select(item => item.TransactionId).ShouldContain(repaymentElsewhere);
        statement.OutstandingTotal.ShouldBe(30m);
    }

    [Fact]
    public async Task Statement_ShouldDenyMembersWithoutAnExplicitTabGrant()
    {
        var terminalOnly = await SeedAsync("FiadoStmtMemberTerminal", Role.Member, grantTerminal: true);
        var noOperator = await factory.SeedFullBranchContextAsync("FiadoStmtMemberNoOp", Role.Member);
        var inactiveTab = await SeedAsync("FiadoStmtMemberInactiveTab", Role.Member);
        var retiredTab = await factory.SeedAccountAsync(inactiveTab.BranchId, AccountType.Tab, "Fiado antigo", active: false);
        await factory.SeedOperatorAccountAsync(inactiveTab.OperatorId, retiredTab.Id);
        var terminalClient = await factory.SeedClientAsync(terminalOnly.BranchId);
        var noOperatorClient = await factory.SeedClientAsync(noOperator.Branch.Id);
        var inactiveClient = await factory.SeedClientAsync(inactiveTab.BranchId);

        foreach (var (clientId, token) in new[]
                 {
                     (terminalClient.Id, terminalOnly.Token),
                     (noOperatorClient.Id, noOperator.Token),
                     (inactiveClient.Id, inactiveTab.Token)
                 })
        {
            var response = await _client.GetAuthAsync($"/client/{clientId}/fiado-statement", token);

            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
                .ShouldContain(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_ACCESS_DENIED);
        }
    }

    [Fact]
    public async Task Statement_ShouldReturn404_ForAnotherBranchClientOrAnUnknownId()
    {
        var ctx = await SeedAsync("FiadoStmtBranchA", Role.Manager);
        var other = await SeedAsync("FiadoStmtBranchB", Role.Manager);
        var foreignClient = await factory.SeedClientAsync(other.BranchId);
        await other.SaleAsync(other.TabA, foreignClient.Id, 10m, _today, _today);

        foreach (var clientId in new[] { foreignClient.Id, Guid.NewGuid() })
        {
            var response = await _client.GetAuthAsync($"/client/{clientId}/fiado-statement", ctx.Token);

            response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
                .ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        }
    }

    [Theory]
    [InlineData("pageSize=101", nameof(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID))]
    [InlineData("pageSize=0", nameof(ResourcesErrorMessages.CLIENT_FIADO_STATEMENT_PAGE_SIZE_INVALID))]
    [InlineData("page=0", nameof(ResourcesErrorMessages.REPORT_PAGE_INVALID))]
    [InlineData("dateFrom=2025-06-10&dateTo=2025-06-01", nameof(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED))]
    public async Task Statement_ShouldReturn400_ForInvalidPagingOrWindow(string query, string key)
    {
        var ctx = await SeedAsync($"FiadoStmt400{key.Length}{query.Length}", Role.Manager);
        var client = await factory.SeedClientAsync(ctx.BranchId);

        var response = await _client.GetAuthAsync($"/client/{client.Id}/fiado-statement?{query}", ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.ResourceManager.GetString(key)!);
    }

    [Fact]
    public async Task Statement_ShouldRejectAWindowInvertedByTheDefaultedEnd()
    {
        var ctx = await SeedAsync("FiadoStmtDefaultInverted", Role.Manager);
        var client = await factory.SeedClientAsync(ctx.BranchId);

        var response = await _client.GetAuthAsync(
            $"/client/{client.Id}/fiado-statement?dateFrom={_today.AddDays(1):yyyy-MM-dd}", ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Fact]
    public async Task Statement_ShouldReturnAnEmptyPage_ForTheLargestValidPageNumber()
    {
        var ctx = await SeedAsync("FiadoStmtHugePage", Role.Manager);
        var client = await factory.SeedClientAsync(ctx.BranchId);
        await ctx.SaleAsync(ctx.TabA, client.Id, 10m, _today, _today);

        var statement = await StatementAsync(client.Id, ctx.Token, $"page={int.MaxValue}&pageSize=100");
        var aging = await _client.GetAuthAsync($"/report/fiado/aging?page={int.MaxValue}&pageSize=100", ctx.Token);

        statement.Items.ShouldBeEmpty();
        statement.TotalCount.ShouldBe(1);
        aging.StatusCode.ShouldBe(HttpStatusCode.OK, await aging.Content.ReadAsStringAsync());
        (await aging.ReadContentAsync<ResponseFiadoAgingJson>()).Items.ShouldBeEmpty();
    }

    private async Task<ResponseClientFiadoStatementJson> StatementAsync(Guid clientId, string token, string query)
    {
        var url = $"/client/{clientId}/fiado-statement" + (query.Length == 0 ? string.Empty : $"?{query}");
        var response = await _client.GetAuthAsync(url, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseClientFiadoStatementJson>();
    }

    private async Task<StatementContext> SeedAsync(
        string label,
        Role role,
        bool grantTabA = false,
        bool grantTerminal = false)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa 1");
        var tabA = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado A");
        var tabB = await factory.SeedAccountAsync(branch.Id, AccountType.Tab, "Fiado B");
        if (grantTabA)
            await factory.SeedOperatorAccountAsync(op.Id, tabA.Id);
        if (grantTerminal)
            await factory.SeedOperatorAccountAsync(op.Id, terminal.Id, isPrimary: true);
        var outCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado saída", Direction.Out);
        var inCategory = await factory.SeedCategoryAsync(branch.Id, "Fiado entrada", Direction.In);
        var saleType = await factory.SeedTransactionTypeAsync(outCategory.Id, "Venda fiado", requiresTabAccountAndClient: true);
        var repaymentType = await factory.SeedTransactionTypeAsync(inCategory.Id, "Recebimento fiado", requiresTabAccountAndClient: true);
        return new StatementContext(factory, branch.Id, user.Id, op.Id, tabA.Id, tabB.Id, outCategory.Id, inCategory.Id, saleType.Id, repaymentType.Id, token);
    }

    private sealed record StatementContext(
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
        public async Task<Guid> SaleAsync(
            Guid tab,
            Guid clientId,
            decimal value,
            DateTime date,
            DateTime dueDate,
            TransactionStatus status = TransactionStatus.Active,
            DateTime? createdAt = null)
        {
            var row = await Factory.SeedTransactionAsync(
                BranchId, tab, SaleTypeId, OutCategoryId, Direction.Out, OperatorId, UserId,
                date: date, value: value, clientId: clientId, dueDate: dueDate, status: status, createdAt: createdAt);
            return row.Id;
        }

        public async Task<Guid> RepaymentAsync(
            Guid tab,
            Guid clientId,
            decimal value,
            DateTime date,
            TransactionStatus status = TransactionStatus.Active,
            DateTime? createdAt = null)
        {
            var row = await Factory.SeedTransactionAsync(
                BranchId, tab, RepaymentTypeId, InCategoryId, Direction.In, OperatorId, UserId,
                date: date, value: value, clientId: clientId, dueDate: date, status: status, createdAt: createdAt);
            return row.Id;
        }
    }
}
