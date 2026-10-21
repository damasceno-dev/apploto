using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

/// <summary>
/// M11 Phase 4.2: open-cheque aging keeps every installment plan exactly as shipped and adds each unpaid Active
/// standalone row of a type classified <c>Cheque</c> (active or retired) as its own one-row group with no invented
/// plan identity. Paid, Draft, cancelled, other-instrument and unclassified standalone rows stay out.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ReportControllerOpenChequeStandaloneTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OpenChequeAging_ShouldListStandaloneCheques_BesidePlans_AcrossThreePages_WithWholeFilterTotals()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAStandalone", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T1");
        var otherTerminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "T2");
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var chequeType = await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque",
            SettlementRule.OperatorEnteredCheque, settlementInstrument: SettlementInstrument.Cheque);
        var retiredChequeType = await factory.SeedTransactionTypeAsync(saidas.Id, "Cheque antigo",
            SettlementRule.OperatorEnteredCheque, active: false, settlementInstrument: SettlementInstrument.Cheque);
        var unclassifiedType = await factory.SeedTransactionTypeAsync(saidas.Id, "Cheque sem classificação",
            SettlementRule.OperatorEnteredCheque);
        var cardType = await factory.SeedTransactionTypeAsync(saidas.Id, "Cartão de Crédito",
            SettlementRule.TwoBusinessDays, settlementInstrument: SettlementInstrument.CreditCard);
        var today = DateTime.Today;

        Task<server.Domain.Entities.Transaction> Row(Guid typeId, Guid accountId, DateTime due, decimal value,
            DateTime? paidAt = null, TransactionStatus status = TransactionStatus.Active, Guid? origin = null, Guid? id = null) =>
            factory.SeedTransactionAsync(branch.Id, accountId, typeId, saidas.Id, Direction.Out, op.Id, user.Id,
                date: today.AddDays(-20), value: value, dueDate: due, paidAt: paidAt, status: status,
                originTransactionId: origin, id: id);

        // Plan of an UNCLASSIFIED type: still a plan, exactly as shipped (oldest open due −10).
        var planId = Guid.NewGuid();
        await Row(unclassifiedType.Id, terminal.Id, today.AddDays(-30), 100m, paidAt: today.AddDays(-29), origin: planId, id: planId);
        await Row(unclassifiedType.Id, terminal.Id, today.AddDays(-10), 100m, origin: planId);
        // Qualifying standalone cheques: active type (due −5) and retired type (due +3).
        var standalone = await Row(chequeType.Id, terminal.Id, today.AddDays(-5), 70m);
        var retiredStandalone = await Row(retiredChequeType.Id, terminal.Id, today.AddDays(3), 40m);
        // Excluded standalone rows.
        await Row(chequeType.Id, terminal.Id, today.AddDays(-7), 11m, paidAt: today.AddDays(-1));
        await Row(chequeType.Id, terminal.Id, today.AddDays(-7), 12m, status: TransactionStatus.Draft);
        await Row(chequeType.Id, terminal.Id, today.AddDays(-7), 13m, status: TransactionStatus.Cancelled);
        await Row(unclassifiedType.Id, terminal.Id, today.AddDays(-7), 14m);
        await Row(cardType.Id, terminal.Id, today.AddDays(-7), 15m);
        // Another account's standalone cheque, outside the account filter.
        var otherAccountCheque = await Row(chequeType.Id, otherTerminal.Id, today.AddDays(-2), 25m);

        var pages = new List<ResponseOpenChequeAgingJson>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await _client.GetAuthAsync(
                $"/report/cheques/open-aging?AccountId={terminal.Id}&Page={page}&PageSize=1", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            pages.Add(await response.ReadContentAsync<ResponseOpenChequeAgingJson>());
        }

        pages.ShouldAllBe(page => page.Items.Count == 1 && page.TotalCount == 3 && page.TotalPages == 3);
        var groups = pages.SelectMany(page => page.Items).ToList();
        groups.Select(group => group.OldestOpenDueDate).ShouldBe([today.AddDays(-10), today.AddDays(-5), today.AddDays(3)]);

        groups[0].OriginTransactionId.ShouldBe(planId);
        groups[0].TotalRowCount.ShouldBe(2);
        groups[0].OpenRowCount.ShouldBe(1);
        groups[1].OriginTransactionId.ShouldBeNull();
        groups[1].Rows.Single().TransactionId.ShouldBe(standalone.Id);
        groups[1].TotalRowCount.ShouldBe(1);
        groups[1].AccountId.ShouldBe(terminal.Id);
        groups[1].Description.ShouldBe(standalone.Description);
        groups[2].OriginTransactionId.ShouldBeNull();
        groups[2].Rows.Single().TransactionId.ShouldBe(retiredStandalone.Id);

        // Whole-filter totals are page-independent and include standalone rows.
        pages.ShouldAllBe(page => page.OutstandingTotal == 210m && page.OpenRowCount == 3);
        groups.Sum(group => group.OutstandingTotal).ShouldBe(210m);
        pages[0].ByBucket.Sum(bucket => bucket.Value).ShouldBe(210m);
        pages[0].ByBucket.Single(bucket => bucket.Bucket == AgingBucket.Current).Value.ShouldBe(40m);

        // Without the account filter the other account's standalone cheque joins as a fourth group.
        var all = await (await _client.GetAuthAsync("/report/cheques/open-aging?Page=1&PageSize=10", token))
            .ReadContentAsync<ResponseOpenChequeAgingJson>();
        all.TotalCount.ShouldBe(4);
        all.OutstandingTotal.ShouldBe(235m);
        all.Items.ShouldContain(group => group.Rows.Single().TransactionId == otherAccountCheque.Id);
    }

    [Fact]
    public async Task OpenChequeAging_ShouldDropAStandaloneCheque_WhenItsTypeIsDeclassified_AndNeverShowAnotherBranchs()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("OCAStandaloneDeclassify");
        var (otherUser, otherBranch, _, _) = await factory.SeedFullBranchContextAsync("OCAStandaloneOther");
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOp = await factory.SeedOperatorAsync(otherBranch.Id, userId: otherUser.Id);
        var terminal = await factory.SeedAccountAsync(branch.Id);
        var otherTerminal = await factory.SeedAccountAsync(otherBranch.Id);
        var saidas = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out);
        var otherSaidas = await factory.SeedCategoryAsync(otherBranch.Id, "Saídas", Direction.Out);
        var chequeType = await factory.SeedTransactionTypeAsync(saidas.Id, "Depósito Cheque",
            SettlementRule.OperatorEnteredCheque, settlementInstrument: SettlementInstrument.Cheque);
        var otherChequeType = await factory.SeedTransactionTypeAsync(otherSaidas.Id, "Depósito Cheque",
            SettlementRule.OperatorEnteredCheque, settlementInstrument: SettlementInstrument.Cheque);
        await factory.SeedTransactionAsync(branch.Id, terminal.Id, chequeType.Id, saidas.Id, Direction.Out, op.Id, user.Id,
            value: 50m, dueDate: DateTime.Today.AddDays(5));
        await factory.SeedTransactionAsync(otherBranch.Id, otherTerminal.Id, otherChequeType.Id, otherSaidas.Id, Direction.Out,
            otherOp.Id, otherUser.Id, value: 999m, dueDate: DateTime.Today.AddDays(5));

        var before = await (await _client.GetAuthAsync("/report/cheques/open-aging?Page=1&PageSize=10", token))
            .ReadContentAsync<ResponseOpenChequeAgingJson>();
        before.TotalCount.ShouldBe(1);
        before.OutstandingTotal.ShouldBe(50m);

        var declassify = await _client.PutAuthAsync($"/transaction-type/{chequeType.Id}",
            new CommonTestUtilities.Requests.RequestUpdateTransactionTypeJsonBuilder()
                .WithName("Depósito Cheque")
                .WithSettlementRule(SettlementRule.OperatorEnteredCheque)
                .WithSettlementInstrument(SettlementInstrument.None)
                .Build(), token);
        declassify.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await (await _client.GetAuthAsync("/report/cheques/open-aging?Page=1&PageSize=10", token))
            .ReadContentAsync<ResponseOpenChequeAgingJson>();
        after.TotalCount.ShouldBe(0);
        after.OutstandingTotal.ShouldBe(0m);
        after.Items.ShouldBeEmpty();
    }
}
