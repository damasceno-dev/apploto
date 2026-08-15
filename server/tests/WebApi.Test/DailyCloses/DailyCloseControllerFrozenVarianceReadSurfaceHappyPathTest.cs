using System.Globalization;
using System.Net;
using CommonTestUtilities.Requests;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerFrozenVarianceReadSurfaceHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(SnapshotRelease.Recall)]
    [InlineData(SnapshotRelease.Reopen)]
    public async Task Release_ShouldHideRetainedVarianceFromOfficialReadSurfaces_AfterLedgerMutation(
        SnapshotRelease release)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcVarianceRelease{release}",
            Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, isPrimary: true);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var date = LocalToday();
        var initialStatus = release == SnapshotRelease.Recall
            ? DailyCloseStatus.Submitted
            : DailyCloseStatus.Approved;
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            initialStatus,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-20),
            approvedByUserId: initialStatus == DailyCloseStatus.Approved ? user.Id : null,
            approvedAt: initialStatus == DailyCloseStatus.Approved
                ? DateTime.UtcNow.AddMinutes(-10)
                : null);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 100m);
        var retainedSnapshot = await factory.SeedDailyCloseItemAsync(
            close.Id,
            cashVarianceProduct.Id,
            125m);

        var releaseResponse = await _client.PostAuthAsync(
            $"/dailyclose/{close.Id}/{release.ToString().ToLowerInvariant()}",
            token);
        releaseResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var released = await releaseResponse.ReadContentAsync<ResponseDailyCloseJson>();
        released.Status.ShouldBe(DailyCloseStatus.Draft);

        var mutationResponse = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(date)
                .WithValue(37m)
                .WithAccountId(account.Id)
                .WithTransactionTypeId(transactionType.Id)
                .Build(),
            token);
        mutationResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var retained = await factory.ReloadAsync<DailyCloseItem>(retainedSnapshot.Id);
        retained.ShouldNotBeNull();
        retained.Active.ShouldBeTrue();
        retained.Value.ShouldBe(125m);

        var reviewResponse = await _client.GetAuthAsync($"/dailyclose/{close.Id}/review", token);
        reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var review = await reviewResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.Status.ShouldBe(DailyCloseStatus.Draft);
        review.Items.Single(item => item.ProductId == cashVarianceProduct.Id)
            .ClosingValue.ShouldBeNull();

        var queryDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dashboardResponse = await _client.GetAuthAsync(
            $"/report/dashboard?date={queryDate}",
            token);
        dashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.ReadContentAsync<ResponseDashboardJson>();
        dashboard.Closes.ShouldNotContain(row => row.DailyCloseId == close.Id);
        var notSubmitted = dashboard.NotSubmitted.Single(row => row.AccountId == account.Id);
        notSubmitted.DailyCloseId.ShouldBe(close.Id);
        notSubmitted.Status.ShouldBe(DailyCloseStatus.Draft);
        dashboard.TotalVariance.ShouldBe(0m);

        var summaryResponse = await _client.GetAuthAsync(
            $"/report/cash-variance?dateFrom={queryDate}&dateTo={queryDate}&accountId={account.Id}&page=1&pageSize=50",
            token);
        summaryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var summary = await summaryResponse.ReadContentAsync<ResponseCashVarianceSummaryJson>();
        summary.Items.ShouldBeEmpty();
        summary.TotalVariance.ShouldBe(0m);

        var monthlyResponse = await _client.GetAuthAsync(
            $"/report/monthly-reconciliation/{date.Year}/{date.Month}",
            token);
        monthlyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var monthly = await monthlyResponse.ReadContentAsync<ResponseMonthlyReconciliationJson>();
        var day = monthly.Days.Single(row => row.Date.Day == date.Day);
        day.NetVariance.ShouldBe(0m);
        var monthlyClose = day.Closes.Single(row => row.DailyCloseId == close.Id);
        monthlyClose.Status.ShouldBe(DailyCloseStatus.Draft);
        monthlyClose.VarianceValue.ShouldBe(0m);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    public enum SnapshotRelease
    {
        Recall,
        Reopen
    }
}
