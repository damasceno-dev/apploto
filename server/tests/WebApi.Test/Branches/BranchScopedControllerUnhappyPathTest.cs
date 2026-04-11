using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches;

/// <summary>
/// Unhappy-path coverage for the Milestone 1 branch endpoints. Drives the real HTTP pipeline
/// to verify the stable error contract across authentication (401), authorization / permission
/// rules (403), missing or cross-branch resources (404), and the last-admin +
/// already-active-membership invariants (409).
/// </summary>
[Collection(ServerApiCollection.Name)]
public class BranchScopedControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 Unauthenticated — missing token on `TokenAuthenticateBranch` filter
    // and on `TokenAuthorize` filter, plus malformed token.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetCurrent_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/branch/current");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task ListUsers_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/branch/users");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task GetCurrent_ShouldReturn401_WhenTokenIsMalformed()
    {
        var httpResponse = await _client.GetAuthAsync("/branch/current", "not-a-real-jwt");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_INVALID);
    }

    // -------------------------------------------------------------------------
    // 403 Forbidden — role-based authorization and permission-matrix rejections.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListUsers_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, memberToken) = await factory.SeedFullBranchContextAsync("ListForbidden", Role.Member);

        var httpResponse = await _client.GetAuthAsync("/branch/users", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task AddUser_ShouldReturn403_WhenManagerAssignsAdminRole()
    {
        var (_, _, _, managerToken) = await factory.SeedFullBranchContextAsync("AddForbidden", Role.Manager);
        var targetUser = await factory.SeedUserAsync();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Admin)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/users", request, managerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task UpdateUserRole_ShouldReturn403_WhenManagerTargetsAdminMembership()
    {
        var (_, branch, _, managerToken) = await factory.SeedFullBranchContextAsync("UpdateForbidden", Role.Manager);
        var adminUser = await factory.SeedUserAsync();
        var adminMembership = await factory.SeedBranchUserAsync(adminUser.Id, branch.Id, Role.Admin);
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/branch/users/{adminMembership.Id}/role", request, managerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    // -------------------------------------------------------------------------
    // 404 Not Found — branch scope isolation and missing entities.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task UpdateUserRole_ShouldReturn404_WhenBranchUserDoesNotExist()
    {
        var (_, _, _, adminToken) = await factory.SeedFullBranchContextAsync("UpdateMissing");
        var unknownBranchUserId = Guid.NewGuid();
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/branch/users/{unknownBranchUserId}/role", request, adminToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
    }

    [Fact]
    public async Task UpdateUserRole_ShouldReturn404_WhenBranchUserBelongsToAnotherBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("Attacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("Victim");
        var victimMember = await factory.SeedUserAsync();
        var victimMembership = await factory.SeedBranchUserAsync(victimMember.Id, victimBranch.Id, Role.Member);
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/branch/users/{victimMembership.Id}/role", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);

        var persisted = await factory.ReloadAsync<BranchUser>(victimMembership.Id);
        persisted.ShouldNotBeNull();
        persisted.Role.ShouldBe(Role.Member);
    }

    [Fact]
    public async Task RemoveUser_ShouldReturn404_WhenBranchUserBelongsToAnotherBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("RemoveAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("RemoveVictim");
        var victimMember = await factory.SeedUserAsync();
        var victimMembership = await factory.SeedBranchUserAsync(victimMember.Id, victimBranch.Id, Role.Member);

        var httpResponse = await _client.DeleteAuthAsync($"/branch/users/{victimMembership.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);

        var persisted = await factory.ReloadAsync<BranchUser>(victimMembership.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSession_ShouldReturn404_WhenUserIsNotMemberOfBranch()
    {
        var strangerUser = await factory.SeedUserAsync();
        var (_, ownedBranch, _, _) = await factory.SeedFullBranchContextAsync("SessionVictim");
        var strangerGlobalToken = factory.IssueGlobalToken(strangerUser);
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(ownedBranch.Id)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/session", request, strangerGlobalToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_NOT_FOUND);
    }

    // -------------------------------------------------------------------------
    // 409 Conflicts — duplicate active membership and last-admin invariant.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AddUser_ShouldReturn409_WhenUserAlreadyHasActiveMembership()
    {
        var (_, branch, _, adminToken) = await factory.SeedFullBranchContextAsync("AddConflict");
        var existingMember = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(existingMember.Id, branch.Id, Role.Member);
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(existingMember.Id)
            .WithEmail(null)
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/users", request, adminToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_ALREADY_ACTIVE);
    }

    [Fact]
    public async Task UpdateUserRole_ShouldReturn409_WhenDemotingLastActiveAdmin()
    {
        var (_, _, adminMembership, adminToken) = await factory.SeedFullBranchContextAsync("LastAdminDemote");
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/branch/users/{adminMembership.Id}/role", request, adminToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);

        var persisted = await factory.ReloadAsync<BranchUser>(adminMembership.Id);
        persisted.ShouldNotBeNull();
        persisted.Role.ShouldBe(Role.Admin);
    }

    [Fact]
    public async Task RemoveUser_ShouldReturn409_WhenRemovingLastActiveAdmin()
    {
        var (_, _, adminMembership, adminToken) = await factory.SeedFullBranchContextAsync("LastAdminRemove");

        var httpResponse = await _client.DeleteAuthAsync($"/branch/users/{adminMembership.Id}", adminToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);

        var persisted = await factory.ReloadAsync<BranchUser>(adminMembership.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }
}
