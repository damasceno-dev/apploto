using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OperatorAccounts;

[Collection(ServerApiCollection.Name)]
public class OperatorAccountControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSelfContext_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/operator/self-context");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestAssignAccountJsonBuilder().Build();

        var httpResponse = await _client.PostAsync($"/operator/{Guid.NewGuid()}/accounts", JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task ListAccounts_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorAccountListForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var httpResponse = await _client.GetAuthAsync($"/operator/{op.Id}/accounts", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorAccountAssignForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task UnassignAccount_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorAccountUnassignForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}/accounts/{account.Id}", memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task SetPrimaryAccount_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("OperatorAccountPrimaryForbidden", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}/accounts/{account.Id}/primary", new { }, memberToken);

        await AssertForbiddenAsync(httpResponse);
    }

    [Fact]
    public async Task ListAccounts_ShouldReturn404_WhenOperatorBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("OperatorAccountListAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("OperatorAccountListVictim");
        var victimOperator = await factory.SeedOperatorAsync(victimBranch.Id);

        var httpResponse = await _client.GetAuthAsync($"/operator/{victimOperator.Id}/accounts", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn404_WhenAccountBelongsToDifferentBranch()
    {
        var (_, branch, _, attackerToken) = await factory.SeedFullBranchContextAsync("OperatorAccountAssignAttacker");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("OperatorAccountAssignVictim");
        var victimAccount = await factory.SeedAccountAsync(victimBranch.Id, AccountType.BankAccount);
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(victimAccount.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task AssignAccount_ShouldReturn409_WhenLinkIsAlreadyActive()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountAssignConflict");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(account.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ACCOUNT_ALREADY_ACTIVE);
    }

    [Fact]
    public async Task UnassignAccount_ShouldReturn404_WhenLinkDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountUnassignMissing");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var httpResponse = await _client.DeleteAuthAsync($"/operator/{op.Id}/accounts/{account.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task SetPrimaryAccount_ShouldReturn404_WhenLinkDoesNotExist()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("OperatorAccountPrimaryMissing");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);

        var httpResponse = await _client.PutAuthAsync($"/operator/{op.Id}/accounts/{account.Id}/primary", new { }, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
    }

    private static async Task AssertForbiddenAsync(HttpResponseMessage httpResponse)
    {
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }
}
