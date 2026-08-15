using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerGetHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ShouldReturn200WithJoinedNamesAndItems_WhenManagerReadsOwnBranchClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcGetMgr", Role.Manager);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id, name: "Mega-Sena");
        var dailyClose = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: DateTime.Today,
            submittedByOperatorId: op.Id);
        var item = await factory.SeedDailyCloseItemAsync(dailyClose.Id, product.Id, value: 250m);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Id.ShouldBe(dailyClose.Id);
        payload.Date.ShouldBe(dailyClose.Date);
        payload.Status.ShouldBe(DailyCloseStatus.Draft);
        payload.AccountId.ShouldBe(account.Id);
        payload.AccountName.ShouldBe(account.Name);
        payload.BranchId.ShouldBe(branch.Id);
        payload.SubmittedByOperatorId.ShouldBe(op.Id);
        payload.SubmittedByOperatorName.ShouldBe(op.Name);
        payload.SubmittedAt.ShouldBeNull();

        payload.Items.ShouldHaveSingleItem();
        var responseItem = payload.Items[0];
        responseItem.Id.ShouldBe(item.Id);
        responseItem.ProductId.ShouldBe(product.Id);
        responseItem.ProductName.ShouldBe(product.Name);
        responseItem.Value.ShouldBe(250m);
        responseItem.CreatedAt.ShouldNotBe(default);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberReadsLinkedAccountClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcGetMember", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: DateTime.Today);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseDailyCloseJson>();
        payload.Id.ShouldBe(dailyClose.Id);
        payload.AccountId.ShouldBe(account.Id);
        payload.AccountName.ShouldBe(account.Name);
        payload.BranchId.ShouldBe(branch.Id);
    }
}
