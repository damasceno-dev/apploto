using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches;

/// <summary>
/// Proves the token-scope boundary for every Milestone 1 branch-only endpoint:
/// a valid global token is rejected with <c>403 Forbidden</c> and the stable
/// permission error payload, while a matching branch-scoped token for the same
/// user/branch is accepted with a 2xx response. Each endpoint runs both halves
/// in a single test so regressions that widen or collapse the scope boundary
/// fail loudly.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class BranchTokenScopeTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetCurrent_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, _, _, branchToken) = await factory.SeedFullBranchContextAsync("ScopeGetCurrent");
        var globalToken = factory.IssueGlobalToken(user);

        var rejected = await _client.GetAuthAsync("/branch/current", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.GetAuthAsync("/branch/current", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ListUsers_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, _, _, branchToken) = await factory.SeedFullBranchContextAsync("ScopeListUsers");
        var globalToken = factory.IssueGlobalToken(user);

        var rejected = await _client.GetAuthAsync("/branch/users", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.GetAuthAsync("/branch/users", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddUser_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, _, _, branchToken) = await factory.SeedFullBranchContextAsync("ScopeAddUser");
        var globalToken = factory.IssueGlobalToken(user);
        var targetUser = await factory.SeedUserAsync();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Member)
            .Build();

        var rejected = await _client.PostAuthAsync("/branch/users", request, globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.PostAuthAsync("/branch/users", request, branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateUserRole_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("ScopeUpdateRole");
        var globalToken = factory.IssueGlobalToken(user);
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var rejected = await _client.PutAuthAsync($"/branch/users/{membership.Id}/role", request, globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.PutAuthAsync($"/branch/users/{membership.Id}/role", request, branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RemoveUser_ShouldRejectGlobalToken_AndAcceptBranchToken()
    {
        var (user, branch, _, branchToken) = await factory.SeedFullBranchContextAsync("ScopeRemoveUser");
        var globalToken = factory.IssueGlobalToken(user);
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);

        var rejected = await _client.DeleteAuthAsync($"/branch/users/{membership.Id}", globalToken);
        rejected.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var rejectedPayload = await rejected.ReadContentAsync<TestResponseErrorJson>();
        rejectedPayload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var accepted = await _client.DeleteAuthAsync($"/branch/users/{membership.Id}", branchToken);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
