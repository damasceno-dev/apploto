using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerMonthlyReconciliationHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturnLockReadyFalse_WhenOneCloseIsDraft()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconHappy", Role.Manager);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var accountA = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal A");
        var accountB = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Terminal B");

        var approvedClose = await factory.SeedDailyCloseAsync(
            branch.Id, accountA.Id, date: new DateTime(2025, 8, 1), status: DailyCloseStatus.Approved);
        var draftClose = await factory.SeedDailyCloseAsync(
            branch.Id, accountB.Id, date: new DateTime(2025, 8, 2), status: DailyCloseStatus.Draft);

        await factory.SeedDailyCloseItemAsync(approvedClose.Id, product.Id, value: 11m);
        await factory.SeedDailyCloseItemAsync(draftClose.Id, product.Id, value: -7m);

        var operatorRow = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var category = await factory.SeedCategoryAsync(branch.Id, "Monthly Recon Category", Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id, "Monthly Recon Type");

        await factory.SeedTransactionAsync(
            branch.Id,
            accountA.Id,
            transactionType.Id,
            category.Id,
            Direction.In,
            operatorRow.Id,
            user.Id,
            date: new DateTime(2025, 8, 1),
            status: TransactionStatus.Active);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/2025/8", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseMonthlyReconciliationJson>();

        payload.Year.ShouldBe(2025);
        payload.Month.ShouldBe(8);
        payload.LockReady.ShouldBeFalse();
        var blocker = payload.Blockers.ShouldHaveSingleItem();
        blocker.Type.ShouldBe(MonthlyReconciliationBlockerType.UnapprovedClose);
        blocker.Day.ShouldBe(2);
        blocker.DailyCloseId.ShouldBe(draftClose.Id);
        blocker.AccountId.ShouldBe(accountB.Id);
        blocker.AccountName.ShouldBe("Terminal B");
        blocker.CloseStatus.ShouldBe(DailyCloseStatus.Draft);
        payload.Days.Count.ShouldBe(31);
        payload.Days[0].ActiveTransactionCount.ShouldBe(1);
        payload.Days[0].NetVariance.ShouldBe(11m);
        payload.Days[1].NetVariance.ShouldBe(0m);
        payload.Days[1].Closes.Single().Status.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task MonthlyReconciliation_ShouldReturnLockReadyTrue_WhenAllClosesApprovedAndNoDraftTransactions()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("MonthlyReconReady", Role.Admin);
        var product = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Ready Terminal");
        var close = await factory.SeedDailyCloseAsync(
            branch.Id, account.Id, date: new DateTime(2025, 9, 1), status: DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, value: 0m);

        var otherBranch = await factory.SeedBranchForOtherContextAsync("MonthlyReconReady-other");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal, "Other Terminal");
        var otherOperator = await factory.SeedOperatorAsync(otherBranch.Id);
        var otherCategory = await factory.SeedCategoryAsync(otherBranch.Id, "Other Monthly Recon Category", Direction.In);
        var otherTransactionType = await factory.SeedTransactionTypeAsync(otherCategory.Id, "Other Monthly Recon Type");
        await factory.SeedTransactionAsync(
            otherBranch.Id,
            otherAccount.Id,
            otherTransactionType.Id,
            otherCategory.Id,
            Direction.In,
            otherOperator.Id,
            user.Id,
            date: new DateTime(2025, 9, 1),
            status: TransactionStatus.Draft);

        var httpResponse = await _client.GetAuthAsync("/report/monthly-reconciliation/2025/9", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseMonthlyReconciliationJson>();

        payload.LockReady.ShouldBeTrue();
        payload.Blockers.ShouldBeEmpty();
        payload.Days.Sum(d => d.DraftTransactionCount).ShouldBe(0);
    }
}
