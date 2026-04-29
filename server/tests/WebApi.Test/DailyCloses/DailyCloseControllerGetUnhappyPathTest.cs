using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerGetUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/dailyclose/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenCloseBelongsToDifferentBranch()
    {
        var (_, branchA, _, tokenA) = await factory.SeedFullBranchContextAsync("DcGetXBranchAttacker", Role.Manager);
        var branchB = await factory.SeedBranchForOtherContextAsync("DcGetXBranchVictim");
        var account = await factory.SeedAccountAsync(branchB.Id, AccountType.Terminal);
        var victimClose = await factory.SeedDailyCloseAsync(branchB.Id, account.Id, date: DateTime.Today);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{victimClose.Id}", tokenA);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcGetMemberNoOp", Role.Member);
        // No operator seeded for this user — member has no linked operator.
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: DateTime.Today);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Get_ShouldReturn403_WhenMemberTargetsUnlinkedAccountClose()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcGetMember403Scope", Role.Member);
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // Operator has no OperatorAccount link to this account.
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var dailyClose = await factory.SeedDailyCloseAsync(branch.Id, account.Id, date: DateTime.Today);

        var httpResponse = await _client.GetAuthAsync($"/dailyclose/{dailyClose.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }
}
