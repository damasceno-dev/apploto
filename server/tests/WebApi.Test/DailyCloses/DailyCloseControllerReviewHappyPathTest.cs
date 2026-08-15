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
        var product = await factory.SeedProductAsync(branch.Id, "Mega-Sena", displayOrder: 20);
        var removedProduct = await factory.SeedProductAsync(
            branch.Id,
            "Removed Product",
            displayOrder: 10,
            active: false);
        var retiredCurrentProduct = await factory.SeedProductAsync(
            branch.Id,
            "Retired Current Product",
            displayOrder: 25,
            active: false);
        var newProduct = await factory.SeedProductAsync(branch.Id, "New Product", displayOrder: 30);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 40);
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
        await factory.SeedDailyCloseItemAsync(mondayClose.Id, retiredCurrentProduct.Id, value: 50m);
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
        payload.Items.Count.ShouldBe(4);
        payload.Items.Select(item => item.ProductId).ShouldBe([
            product.Id,
            retiredCurrentProduct.Id,
            newProduct.Id,
            cashVarianceProduct.Id
        ]);

        var normalItem = payload.Items.Single(item => item.ProductId == product.Id);
        normalItem.ProductName.ShouldBe(product.Name);
        normalItem.DisplayOrder.ShouldBe(product.DisplayOrder);
        normalItem.OpeningValue.ShouldBe(140m);
        normalItem.ClosingValue.ShouldBe(180m);
        normalItem.IsCashVarianceProduct.ShouldBeFalse();

        var retiredCurrentItem = payload.Items.Single(item =>
            item.ProductId == retiredCurrentProduct.Id);
        retiredCurrentItem.OpeningValue.ShouldBe(0m);
        retiredCurrentItem.ClosingValue.ShouldBe(50m);

        var newItem = payload.Items.Single(item => item.ProductId == newProduct.Id);
        newItem.OpeningValue.ShouldBe(0m);
        newItem.ClosingValue.ShouldBeNull();

        var cashVarianceItem = payload.Items.Single(item => item.ProductId == cashVarianceProduct.Id);
        cashVarianceItem.DisplayOrder.ShouldBe(cashVarianceProduct.DisplayOrder);
        cashVarianceItem.OpeningValue.ShouldBeNull();
        cashVarianceItem.ClosingValue.ShouldBeNull();
        cashVarianceItem.IsCashVarianceProduct.ShouldBeTrue();
        payload.Items.ShouldNotContain(item => item.ProductId == removedProduct.Id);
    }

    [Fact]
    public async Task Review_ShouldReturn200_WhenAdminReadsOwnBranchClose()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewAdmin", Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var firstProduct = await factory.SeedProductAsync(branch.Id, "Second", displayOrder: 20);
        var secondProduct = await factory.SeedProductAsync(branch.Id, "First", displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 30);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        payload.Id.ShouldBe(dailyClose.Id);
        payload.Items.Select(item => item.ProductId).ShouldBe([
            secondProduct.Id,
            firstProduct.Id,
            cashVarianceProduct.Id
        ]);
        payload.Items.ShouldAllBe(item => item.ClosingValue == null);
        payload.Items.Single(item => item.ProductId == cashVarianceProduct.Id).OpeningValue.ShouldBeNull();
        payload.Items.Where(item => item.ProductId != cashVarianceProduct.Id)
            .ShouldAllBe(item => item.OpeningValue == 0m);
    }

    [Fact]
    public async Task Review_ShouldReturn200_WhenMemberReadsLinkedAccountClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewMember", Role.Member);
        var callerOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, "Telesena");
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
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
        payload.Items.Single(item => item.ProductId == product.Id).OpeningValue.ShouldBe(40m);
    }

    [Fact]
    public async Task Review_ShouldNotUsePriorCloseFromAnotherBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcReviewIsolation", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcReviewIsolationOther");
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, "Lotofácil");
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
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
        payload.Items.Single(item => item.ProductId == product.Id).OpeningValue.ShouldBe(0m);
    }
}
