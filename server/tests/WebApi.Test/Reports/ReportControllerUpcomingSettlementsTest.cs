using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 5: <c>GET /report/upcoming-settlements</c> against real PostgreSQL under a frozen branch clock — window
/// edges, exclusions, <c>DueDate, Date, Id</c> order across three pages, whole-filter and per-instrument totals,
/// overdue as an independent scope, filters, plan rows once, rename/retire, configuration states, auth and isolation.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerUpcomingSettlementsTest(ServerWebApplicationFactory factory)
{
    private static readonly DateTime T = new(2026, 10, 20);
    private readonly HttpClient _client = factory.CreateClient();

    private sealed record Fixture(
        Branch Branch,
        string Token,
        Account T1,
        Account T2Retired,
        TransactionType Credit,
        TransactionType Debit,
        TransactionType Cheque,
        Guid PlanId,
        Dictionary<string, Transaction> Rows);

    private async Task<Fixture> SeedAsync(string label)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var t1 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T1");
        var t2 = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T2 retired", active: false);
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var credit = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Crédito", SettlementRule.TwoBusinessDays,
            settlementInstrument: SettlementInstrument.CreditCard);
        var debit = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Débito", SettlementRule.NextBusinessDay,
            settlementInstrument: SettlementInstrument.DebitCard);
        var cheque = await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque,
            settlementInstrument: SettlementInstrument.Cheque);
        var pix = await factory.SeedTransactionTypeAsync(saidas.Id, "PIX");
        var rows = new Dictionary<string, Transaction>();
        var planId = Guid.NewGuid();

        async Task Row(string key, TransactionType type, int due, decimal value, int date = -10, Account? account = null,
            DateTime? paidAt = null, TransactionStatus status = TransactionStatus.Active, Guid? origin = null, Guid? id = null,
            bool active = true)
        {
            rows[key] = await factory.SeedTransactionAsync(branch.Id, (account ?? t1).Id, type.Id, saidas.Id, Direction.Out,
                op.Id, user.Id, date: T.AddDays(date), value: value, dueDate: T.AddDays(due), paidAt: paidAt, status: status,
                originTransactionId: origin, id: id, active: active);
        }

        await Row("c3", credit, 0, 30m);                         // edge: due on AsOfDate → in
        await Row("c1", credit, 1, 10m);
        await Row("c2", credit, 14, 20m);                        // due AsOfDate + 14 → outside [T, T+13]
        await Row("c4", debit, 13, 40m);                         // edge: due AsOfDate + 13 → in
        await Row("d1", debit, 7, 5m, date: -5);                 // same due date: Date breaks the tie
        await Row("d2", debit, 7, 6m, date: -6);
        await Row("p1", cheque, 5, 50m, origin: planId, id: planId); // plan row in window
        await Row("p2", cheque, 35, 60m, origin: planId);         // plan row beyond the window
        await Row("p3", cheque, -3, 70m, origin: planId);         // plan row overdue
        await Row("s1", cheque, -1, 80m);                         // standalone cheque overdue
        await Row("t2", credit, 3, 15m, account: t2);              // deactivated account, still reported
        await Row("paid", credit, 2, 99m, paidAt: T.AddDays(-1));
        await Row("draft", debit, 2, 98m, status: TransactionStatus.Draft);
        await Row("cancelled", debit, 2, 97m, status: TransactionStatus.Cancelled);
        await Row("deleted", debit, 2, 96m, active: false);
        await Row("pix", pix, 2, 95m);

        return new Fixture(branch, token, t1, t2, credit, debit, cheque, planId, rows);
    }

    private async Task<ResponseUpcomingSettlementsJson> Read(string token, string query)
    {
        var response = await _client.GetAuthAsync($"/report/upcoming-settlements?{query}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseUpcomingSettlementsJson>();
    }

    private static string Window(int from = 0, int to = 13) => $"DueDateFrom={T.AddDays(from):yyyy-MM-dd}&DueDateTo={T.AddDays(to):yyyy-MM-dd}";

    [Fact]
    public async Task UpcomingSettlements_ShouldOrderFilterAndTotal_AcrossThreePages_WithOverdueKeptApart()
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(10));
        var fixture = await SeedAsync("UpcomingFull");

        var pages = new List<ResponseUpcomingSettlementsJson>();
        for (var page = 1; page <= 3; page++)
            pages.Add(await Read(fixture.Token, $"{Window()}&Page={page}&PageSize=3"));

        pages.Select(page => page.Items.Count).ShouldBe([3, 3, 1]);
        var items = pages.SelectMany(page => page.Items).ToList();
        items.Select(item => item.TransactionId).ShouldBe(new[] { "c3", "c1", "t2", "p1", "d2", "d1", "c4" }
            .Select(key => fixture.Rows[key].Id));
        foreach (var page in pages)
        {
            page.AsOfDate.ShouldBe(T);
            page.IsConfigured.ShouldBeTrue();
            page.DueDateFrom.ShouldBe(T);
            page.DueDateTo.ShouldBe(T.AddDays(13));
            page.TotalCount.ShouldBe(7);
            page.TotalValue.ShouldBe(156m);
            page.TotalPages.ShouldBe(3);
            page.OverdueCount.ShouldBe(2);
            page.OverdueValue.ShouldBe(150m);
            page.ByInstrument.Select(total => (total.Instrument, total.Count, total.Value)).ShouldBe([
                (SettlementInstrument.Cheque, 1, 50m),
                (SettlementInstrument.CreditCard, 3, 55m),
                (SettlementInstrument.DebitCard, 3, 51m)
            ]);
        }
        items.Sum(item => item.Value).ShouldBe(pages[0].TotalValue);
        pages[0].HasPrevious.ShouldBeFalse();
        pages[2].HasNext.ShouldBeFalse();

        // The plan row keeps its plan identity and is counted once; dates keep their meaning.
        var planRow = items.Single(item => item.TransactionId == fixture.PlanId);
        planRow.OriginTransactionId.ShouldBe(fixture.PlanId);
        planRow.Instrument.ShouldBe(SettlementInstrument.Cheque);
        planRow.Date.ShouldBe(T.AddDays(-10));
        planRow.DueDate.ShouldBe(T.AddDays(5));
        items.Single(item => item.TransactionId == fixture.Rows["c1"].Id).OriginTransactionId.ShouldBeNull();
        items.Single(item => item.TransactionId == fixture.Rows["t2"].Id).AccountName.ShouldBe("T2 retired");

        // Account filter (applied before paging, overdue included).
        var t1Only = await Read(fixture.Token, $"{Window()}&AccountId={fixture.T1.Id}&Page=1&PageSize=50");
        t1Only.TotalCount.ShouldBe(6);
        t1Only.TotalValue.ShouldBe(141m);
        t1Only.OverdueCount.ShouldBe(2);
        t1Only.Items.ShouldAllBe(item => item.AccountId == fixture.T1.Id);
        var retiredAccount = await Read(fixture.Token, $"{Window()}&AccountId={fixture.T2Retired.Id}&Page=1&PageSize=50");
        retiredAccount.TotalCount.ShouldBe(1);
        retiredAccount.OverdueCount.ShouldBe(0);

        // Instrument filter narrows the window and the overdue scope alike.
        var chequesOnly = await Read(fixture.Token, $"{Window()}&Instrument=Cheque&Page=1&PageSize=50");
        chequesOnly.TotalCount.ShouldBe(1);
        chequesOnly.TotalValue.ShouldBe(50m);
        chequesOnly.OverdueCount.ShouldBe(2);
        chequesOnly.OverdueValue.ShouldBe(150m);
        chequesOnly.ByInstrument.Where(total => total.Instrument != SettlementInstrument.Cheque).ShouldAllBe(total => total.Count == 0);

        // A window that starts before today also lists overdue rows; the overdue figure stays its own scope.
        var wide = await Read(fixture.Token, $"{Window(-30, 40)}&Page=1&PageSize=50");
        wide.TotalCount.ShouldBe(11);
        wide.TotalValue.ShouldBe(156m + 20m + 60m + 150m);
        wide.OverdueValue.ShouldBe(150m);
    }

    [Fact]
    public async Task UpcomingSettlements_ShouldKeepRowsOfRenamedAndRetiredTypes_AndStayConfigured()
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(10));
        var fixture = await SeedAsync("UpcomingRetire");

        var rename = await _client.PutAuthAsync($"/transaction-type/{fixture.Credit.Id}", new RequestUpdateTransactionTypeJsonBuilder()
            .WithName("Crédito — adquirente")
            .WithSettlementRule(SettlementRule.TwoBusinessDays)
            .Build(), fixture.Token);
        rename.StatusCode.ShouldBe(HttpStatusCode.OK);
        foreach (var type in new[] { fixture.Credit, fixture.Debit, fixture.Cheque })
            (await _client.DeleteAuthAsync($"/transaction-type/{type.Id}", fixture.Token)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var report = await Read(fixture.Token, $"{Window()}&Page=1&PageSize=50");

        report.IsConfigured.ShouldBeTrue();
        report.TotalCount.ShouldBe(7);
        report.TotalValue.ShouldBe(156m);
        report.Items.Where(item => item.Instrument == SettlementInstrument.CreditCard)
            .ShouldAllBe(item => item.TransactionTypeName == "Crédito — adquirente");
    }

    [Fact]
    public async Task UpcomingSettlements_ShouldDistinguishNotConfigured_FromConfiguredButEmpty()
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(10));
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("UpcomingNotConfigured");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id);
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var card = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Crédito", SettlementRule.TwoBusinessDays);
        await factory.SeedTransactionAsync(branch.Id, account.Id, card.Id, saidas.Id, Direction.Out, op.Id, user.Id,
            date: T, value: 10m, dueDate: T.AddDays(2));

        var notConfigured = await Read(token, $"{Window()}&Page=1&PageSize=50");
        notConfigured.IsConfigured.ShouldBeFalse();
        notConfigured.TotalCount.ShouldBe(0);
        notConfigured.Items.ShouldBeEmpty();

        // Classifying a different type with no open rows configures the report; it is now truthfully empty.
        await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque,
            active: false, settlementInstrument: SettlementInstrument.Cheque);
        var configuredEmpty = await Read(token, $"{Window()}&Page=1&PageSize=50");
        configuredEmpty.IsConfigured.ShouldBeTrue();
        configuredEmpty.TotalCount.ShouldBe(0);
        configuredEmpty.OverdueCount.ShouldBe(0);
    }

    [Fact]
    public async Task UpcomingSettlements_ShouldEnforceRolesValidationAndBranchIsolation()
    {
        using var clock = factory.FreezeBranchLocalTime(T.AddHours(10));
        var fixture = await SeedAsync("UpcomingIsolation");
        var other = await SeedAsync("UpcomingIsolationOther");
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("UpcomingMember", Role.Member);
        var (_, _, _, adminToken) = await factory.SeedFullBranchContextAsync("UpcomingAdmin");

        var mine = await Read(fixture.Token, $"{Window()}&Page=1&PageSize=50");
        mine.Items.ShouldNotContain(item => other.Rows.Values.Select(row => row.Id).Contains(item.TransactionId));
        mine.TotalValue.ShouldBe(156m);

        (await _client.GetAsync($"/report/upcoming-settlements?{Window()}&Page=1&PageSize=50")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.GetAuthAsync($"/report/upcoming-settlements?{Window()}&Page=1&PageSize=50", memberToken)).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
        (await _client.GetAuthAsync($"/report/upcoming-settlements?{Window()}&Page=1&PageSize=50", adminToken)).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        var crossBranchAccount = await _client.GetAuthAsync(
            $"/report/upcoming-settlements?{Window()}&AccountId={other.T1.Id}&Page=1&PageSize=50", fixture.Token);
        crossBranchAccount.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await crossBranchAccount.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var tooWide = await _client.GetAuthAsync($"/report/upcoming-settlements?{Window(0, 367)}&Page=1&PageSize=50", fixture.Token);
        tooWide.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooWide.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);

        var noneFilter = await _client.GetAuthAsync($"/report/upcoming-settlements?{Window()}&Instrument=None&Page=1&PageSize=50", fixture.Token);
        noneFilter.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noneFilter.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldBe([ResourcesErrorMessages.REPORT_SETTLEMENT_INSTRUMENT_INVALID]);

        (await _client.GetAuthAsync($"/report/upcoming-settlements?Page=1&PageSize=50", fixture.Token)).StatusCode
            .ShouldBe(HttpStatusCode.BadRequest);
    }
}
