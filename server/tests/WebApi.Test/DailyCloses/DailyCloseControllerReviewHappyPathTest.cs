using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerReviewHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Review_ShouldReturn200WithMostRecentOpeningValues_WhenManagerReadsClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewMgr", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, "Mega-Sena");
        var removedProduct = await factory.SeedProductAsync(branch.Id, "Removed Product");
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var thursdayClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 7, 2));
        await factory.SeedDailyCloseItemAsync(thursdayClose.Id, product.Id, value: 100m);
        var fridayClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 7, 3));
        await factory.SeedDailyCloseItemAsync(fridayClose.Id, product.Id, value: 140m);
        await factory.SeedDailyCloseItemAsync(fridayClose.Id, removedProduct.Id, value: 30m);
        await factory.SeedDailyCloseItemAsync(fridayClose.Id, cashVarianceProduct.Id, value: 999m);
        var mondayClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 7, 6),
            submittedByOperatorId: null);
        await factory.SeedDailyCloseItemAsync(mondayClose.Id, product.Id, value: 180m);
        await factory.SeedDailyCloseItemAsync(mondayClose.Id, cashVarianceProduct.Id, value: -5m);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{mondayClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        payload.Id.ShouldBe(mondayClose.Id);
        payload.Date.ShouldBe(mondayClose.Date);
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.AccountId.ShouldBe(account.Id);
        payload.AccountName.ShouldBe(account.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.Items.Count.ShouldBe(2);

        var normalItem = payload.Items.Single(item => item.ProductId == product.Id);
        normalItem.ProductName.ShouldBe(product.Name);
        normalItem.OpeningValue.ShouldBe(140m);
        normalItem.ClosingValue.ShouldBe(180m);
        normalItem.IsCashVarianceProduct.ShouldBeFalse();

        var cashVarianceItem = payload.Items.Single(item => item.ProductId == cashVarianceProduct.Id);
        cashVarianceItem.OpeningValue.ShouldBeNull();
        cashVarianceItem.ClosingValue.ShouldBe(-5m);
        cashVarianceItem.IsCashVarianceProduct.ShouldBeTrue();
        payload.Items.ShouldNotContain(item => item.ProductId == removedProduct.Id);
    }

    [Fact]
    public async Task Review_ShouldReturn200_WhenAdminReadsOwnBranchClose()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewAdmin", Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        payload.Id.ShouldBe(dailyClose.Id);
        payload.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Review_ShouldReturn200_WhenMemberReadsLinkedAccountClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewMember", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, "Telesena");
        var priorClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 6, 30));
        await factory.SeedDailyCloseItemAsync(priorClose.Id, product.Id, 40m);
        var currentClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 7, 2));
        await factory.SeedDailyCloseItemAsync(currentClose.Id, product.Id, 55m);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{currentClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        payload.Id.ShouldBe(currentClose.Id);
        payload.Items.ShouldHaveSingleItem().OpeningValue.ShouldBe(40m);
    }

    [Fact]
    public async Task Review_ShouldNotUsePriorCloseFromAnotherBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewIsolation", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcReviewIsolationOther");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, "Lotofácil");
        var crossBranchPriorClose = await factory.SeedDailyCloseAsync(
            otherBranch.Id,
            account.Id,
            date: new DateTime(2026, 7, 1));
        await factory.SeedDailyCloseItemAsync(crossBranchPriorClose.Id, product.Id, 777m);
        var currentClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: new DateTime(2026, 7, 2));
        await factory.SeedDailyCloseItemAsync(currentClose.Id, product.Id, 80m);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{currentClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        payload.Items.ShouldHaveSingleItem().OpeningValue.ShouldBe(0m);
    }
}
