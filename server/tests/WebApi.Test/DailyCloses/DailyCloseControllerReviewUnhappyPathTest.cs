using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerReviewUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Review_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/dailyclose/{Guid.NewGuid()}/review");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Review_ShouldReturn404_WhenCloseDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcReviewMissing", Role.Manager);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{Guid.NewGuid()}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Review_ShouldReturn404_WhenCloseBelongsToDifferentBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("DcReviewXBranchAttacker", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcReviewXBranchVictim");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherClose = await factory.SeedDailyCloseAsync(otherBranch.Id, otherAccount.Id);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{otherClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Review_ShouldReturn403_WhenMemberTargetsUnlinkedAccountClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcReviewMemberScope",
            Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}/review", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }
}
