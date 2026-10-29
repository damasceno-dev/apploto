using System.Net;
using server.Application.Services.Transactions;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

/// <summary>
/// M7.7 Phase 7.6.1: the history read uses the close's own read permission, exactly like
/// <c>GET /session/{id}</c>.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerHistoryUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task History_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await _client.GetAsync($"/session/{Guid.NewGuid()}/history");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task History_ShouldReturn403_WhenMemberHasNoOperatorLink()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HistoryNoOperator", Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, Today().AddDays(-1));

        var response = await _client.GetAuthAsync($"/session/{close.Id}/history", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task History_ShouldReturn403_WhenMemberAccountIsOutOfScope()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("HistoryOutOfScope", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, Today().AddDays(-1));

        var response = await _client.GetAuthAsync($"/session/{close.Id}/history", token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task History_ShouldReturn404_WhenCloseBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HistoryCrossBranch", Role.Manager);
        var (_, otherBranch, _, _) = await factory.SeedFullBranchContextAsync("HistoryCrossBranchOther", Role.Manager);
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var foreignClose = await factory.SeedDailyCloseAsync(otherBranch.Id, otherAccount.Id, Today().AddDays(-1));

        var foreign = await _client.GetAuthAsync($"/session/{foreignClose.Id}/history", token);
        var missing = await _client.GetAuthAsync($"/session/{Guid.NewGuid()}/history", token);

        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await foreign.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static DateTime Today() => new BranchClock().LocalBusinessDate(DateTime.UtcNow);
}
