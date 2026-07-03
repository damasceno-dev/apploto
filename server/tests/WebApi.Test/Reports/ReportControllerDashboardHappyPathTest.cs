using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerDashboardHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // Mixed day: Submitted + Approved + Rejected closes, one expected account
    // without a close, variance joined per (Date, AccountId)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldReturnMixedDayProjection_WhenClosesVarianceAndAssignmentsSeeded()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashHappy1", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal A");
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal B");
        var accountC = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal C");
        var accountD = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal D");

        var operatorA = await factory.SeedOperatorAsync(branch.Id, "Operator A");
        var operatorB = await factory.SeedOperatorAsync(branch.Id, "Operator B");
        var operatorC = await factory.SeedOperatorAsync(branch.Id, "Operator C");
        var operatorD = await factory.SeedOperatorAsync(branch.Id, "Operator D");
        await factory.SeedOperatorAccountAsync(operatorA.Id, accountA.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(operatorB.Id, accountB.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(operatorC.Id, accountC.Id, isPrimary: true);
        await factory.SeedOperatorAccountAsync(operatorD.Id, accountD.Id, isPrimary: true);

        var date = new DateTime(2025, 3, 10);
        var closeA = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: date, status: DailyCloseStatus.Submitted,
            submittedByOperatorId: operatorA.Id, submittedAt: date.AddHours(18));
        var closeB = await factory.SeedDailyCloseAsync(
            branch.Id, accountB.Id, date: date, status: DailyCloseStatus.Approved,
            submittedByOperatorId: operatorB.Id, submittedAt: date.AddHours(18), approvedAt: date.AddHours(20));
        var closeC = await factory.SeedDailyCloseAsync(
            branch.Id, accountC.Id, date: date, status: DailyCloseStatus.Rejected,
            submittedByOperatorId: operatorC.Id, submittedAt: date.AddHours(18));

        var varianceItemA = await factory.SeedDailyCloseItemAsync(closeA.Id, product.Id, value: -30m);
        await factory.SeedDailyCloseItemAsync(closeB.Id, product.Id, value: 150m);
        await factory.SeedDailyCloseItemAsync(closeC.Id, product.Id, value: 20m);

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-10", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDashboardJson>();

        payload.Date.ShouldBe(date);
        payload.Closes.Count.ShouldBe(3);

        var rowA = payload.Closes.Single(c => c.AccountId == accountA.Id);
        rowA.DailyCloseId.ShouldBe(closeA.Id);
        rowA.AccountName.ShouldBe("Terminal A");
        rowA.SubmittedByOperatorId.ShouldBe(operatorA.Id);
        rowA.SubmittedByOperatorName.ShouldBe("Operator A");
        rowA.Status.ShouldBe(DailyCloseStatus.Submitted);
        rowA.SubmittedAt.ShouldNotBeNull();
        rowA.ApprovedAt.ShouldBeNull();

        // Reload-based assertion: the projected variance equals the persisted item value.
        var reloadedVarianceA = await factory.ReloadAsync<server.Domain.Entities.DailyCloseItem>(varianceItemA.Id);
        reloadedVarianceA.ShouldNotBeNull();
        rowA.VarianceValue.ShouldBe(reloadedVarianceA.Value);

        var rowB = payload.Closes.Single(c => c.AccountId == accountB.Id);
        rowB.Status.ShouldBe(DailyCloseStatus.Approved);
        rowB.ApprovedAt.ShouldNotBeNull();
        rowB.VarianceValue.ShouldBe(150m);

        var rowC = payload.Closes.Single(c => c.AccountId == accountC.Id);
        rowC.Status.ShouldBe(DailyCloseStatus.Rejected);
        rowC.VarianceValue.ShouldBe(20m);

        payload.PendingApprovalCount.ShouldBe(1);

        var notSubmitted = payload.NotSubmitted.ShouldHaveSingleItem();
        notSubmitted.AccountId.ShouldBe(accountD.Id);
        notSubmitted.AccountName.ShouldBe("Terminal D");
        notSubmitted.OperatorId.ShouldBe(operatorD.Id);
        notSubmitted.OperatorName.ShouldBe("Operator D");
        notSubmitted.DailyCloseId.ShouldBeNull();
        notSubmitted.Status.ShouldBeNull();

        payload.TotalVariance.ShouldBe(140m);
        payload.MeanVariance.ShouldBe(140m / 3m);
    }

    // -------------------------------------------------------------------------
    // Draft close: not submitted, but carries the close id for deep-linking
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldCarryDraftCloseId_WhenExpectedAccountHasDraftClose()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashHappy2", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal Draft");
        var op = await factory.SeedOperatorAsync(branch.Id, "Operator Draft");
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, isPrimary: true);

        var date = new DateTime(2025, 3, 11);
        var draftClose = await factory.SeedDailyCloseAsync(
            branch.Id, account.Id, date: date, status: DailyCloseStatus.Draft);

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-11", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDashboardJson>();

        payload.Closes.ShouldBeEmpty();
        payload.PendingApprovalCount.ShouldBe(0);

        var notSubmitted = payload.NotSubmitted.ShouldHaveSingleItem();
        notSubmitted.AccountId.ShouldBe(account.Id);
        notSubmitted.DailyCloseId.ShouldBe(draftClose.Id);
        notSubmitted.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    // -------------------------------------------------------------------------
    // Cross-branch isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldExcludeOtherBranchData_WhenAnotherBranchHasSameDayActivity()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashHappy3", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DashHappy3-other");

        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var otherProduct = await factory.SeedProductAsync(otherBranch.Id, "Diferença Caixa");

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Own Terminal");
        var op = await factory.SeedOperatorAsync(branch.Id, "Own Operator");
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, isPrimary: true);

        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal, "Other Terminal");
        var otherOperator = await factory.SeedOperatorAsync(otherBranch.Id, "Other Operator");
        await factory.SeedOperatorAccountAsync(otherOperator.Id, otherAccount.Id, isPrimary: true);

        var date = new DateTime(2025, 3, 12);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id, account.Id, date: date, status: DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id, submittedAt: date.AddHours(18));
        var otherClose = await factory.SeedDailyCloseAsync(
            otherBranch.Id, otherAccount.Id, date: date, status: DailyCloseStatus.Submitted,
            submittedByOperatorId: otherOperator.Id, submittedAt: date.AddHours(18));
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 100m);
        await factory.SeedDailyCloseItemAsync(otherClose.Id, otherProduct.Id, value: 999m);

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-12", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDashboardJson>();

        var row = payload.Closes.ShouldHaveSingleItem();
        row.AccountId.ShouldBe(account.Id);
        row.VarianceValue.ShouldBe(100m);
        payload.TotalVariance.ShouldBe(100m);
        payload.NotSubmitted.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Operator identity tiebreak on multi-operator accounts: primary link wins,
    // otherwise first active link by operator name
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldPickPrimaryThenAlphabeticalOperator_WhenAccountHasMultipleLinks()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashHappy5", Role.Manager);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        // Primary wins even when it is alphabetically last and linked last.
        var primaryAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal Primary");
        var alberto = await factory.SeedOperatorAsync(branch.Id, "Alberto");
        var zulmira = await factory.SeedOperatorAsync(branch.Id, "Zulmira");
        await factory.SeedOperatorAccountAsync(alberto.Id, primaryAccount.Id, isPrimary: false);
        await factory.SeedOperatorAccountAsync(zulmira.Id, primaryAccount.Id, isPrimary: true);

        // No primary link: alphabetical order decides, not insertion order and not id order.
        // Postgres orders uuid columns by their byte sequence, which matches ordinal ordering of the
        // canonical hex string; give Carla the smaller id so the trailing ThenBy(OperatorId) fallback
        // would pick her — only ThenBy(Operator.Name) selects Beatriz.
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var (carlaId, beatrizId) = string.CompareOrdinal(idA.ToString(), idB.ToString()) < 0
            ? (idA, idB)
            : (idB, idA);

        var alphaAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal Alpha");
        var carla = await factory.SeedOperatorAsync(branch.Id, "Carla", id: carlaId);
        var beatriz = await factory.SeedOperatorAsync(branch.Id, "Beatriz", id: beatrizId);
        await factory.SeedOperatorAccountAsync(carla.Id, alphaAccount.Id, isPrimary: false);
        await factory.SeedOperatorAccountAsync(beatriz.Id, alphaAccount.Id, isPrimary: false);

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-14", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDashboardJson>();

        payload.NotSubmitted.Count.ShouldBe(2);

        var primaryRow = payload.NotSubmitted.Single(n => n.AccountId == primaryAccount.Id);
        primaryRow.OperatorId.ShouldBe(zulmira.Id);
        primaryRow.OperatorName.ShouldBe("Zulmira");

        var alphaRow = payload.NotSubmitted.Single(n => n.AccountId == alphaAccount.Id);
        alphaRow.OperatorId.ShouldBe(beatriz.Id);
        alphaRow.OperatorName.ShouldBe("Beatriz");
    }

    // -------------------------------------------------------------------------
    // Empty day: zero aggregates; only linked terminals count as expected
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Dashboard_ShouldReturnZeroAggregatesAndExpectedOnlyNotSubmitted_WhenDayIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DashHappy4", Role.Admin);
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");

        var linkedTerminal = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Linked Terminal");
        var op = await factory.SeedOperatorAsync(branch.Id, "Linked Operator");
        await factory.SeedOperatorAccountAsync(op.Id, linkedTerminal.Id, isPrimary: true);

        // Neither of these is expected to close: banks are outside §6.5 daily closing and an
        // unlinked terminal has no operator assignment.
        await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount, "Bank Account");
        await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Unlinked Terminal");

        var httpResponse = await _client.GetAuthAsync("/report/dashboard?date=2025-03-13", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDashboardJson>();

        payload.Closes.ShouldBeEmpty();
        payload.PendingApprovalCount.ShouldBe(0);
        payload.TotalVariance.ShouldBe(0m);
        payload.MeanVariance.ShouldBe(0m);

        var notSubmitted = payload.NotSubmitted.ShouldHaveSingleItem();
        notSubmitted.AccountId.ShouldBe(linkedTerminal.Id);
        notSubmitted.OperatorId.ShouldBe(op.Id);
        notSubmitted.OperatorName.ShouldBe("Linked Operator");
    }
}
