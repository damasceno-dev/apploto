using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Operators;

[Collection(ServerApiCollection.Name)]
public class OperatorTokenScopeTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetSelfContext_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, _, _, branchToken) = await factory.SeedFullBranchContextAsync("OperatorSelfScope", Role.Member);
        var globalToken = factory.IssueGlobalToken(user);

        var rejected = await _client.GetAuthAsync("/operator/self-context", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.GetAuthAsync("/operator/self-context", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListAccounts_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("OperatorListAccountsScope", Role.Manager);
        var globalToken = factory.IssueGlobalToken(user);
        var op = await factory.SeedOperatorAsync(branch.Id);

        var rejected = await _client.GetAuthAsync($"/operator/{op.Id}/accounts", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.GetAuthAsync($"/operator/{op.Id}/accounts", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AssignAccount_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("OperatorAssignScope", Role.Manager);
        var globalToken = factory.IssueGlobalToken(user);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(account.Id)
            .Build();

        var rejected = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.PostAuthAsync($"/operator/{op.Id}/accounts", request, branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnassignAccount_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("OperatorUnassignScope", Role.Manager);
        var globalToken = factory.IssueGlobalToken(user);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);

        var rejected = await _client.DeleteAuthAsync($"/operator/{op.Id}/accounts/{account.Id}", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.DeleteAuthAsync($"/operator/{op.Id}/accounts/{account.Id}", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SetPrimaryAccount_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("OperatorSetPrimaryScope", Role.Manager);
        var globalToken = factory.IssueGlobalToken(user);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);

        var rejected = await _client.PutAuthAsync($"/operator/{op.Id}/accounts/{account.Id}/primary", new { }, globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.PutAuthAsync($"/operator/{op.Id}/accounts/{account.Id}/primary", new { }, branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
