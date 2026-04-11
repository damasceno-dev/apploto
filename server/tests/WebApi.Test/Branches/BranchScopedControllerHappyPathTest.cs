using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches;

/// <summary>
/// Happy-path coverage for the Milestone 1 Branch endpoints that require a branch-scoped
/// token: <c>GET /branch/current</c>, <c>GET /branch/users</c>, <c>POST /branch/users</c>,
/// <c>PUT /branch/users/{id}/role</c>, and <c>DELETE /branch/users/{id}</c>.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class BranchScopedControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetCurrent_ShouldReturnOkWithBranchSummary()
    {
        var (_, branch, branchUser, token) = await SeedAdminMembershipAsync("Current");

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/branch/current");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpResponse = await _client.SendAsync(httpRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.Content.ReadFromJsonAsync<ResponseGetCurrentBranchSummaryJson>();
        payload.ShouldNotBeNull();
        payload!.Branch.ShouldNotBeNull();
        payload.Branch.Id.ShouldBe(branch.Id);
        payload.Branch.Name.ShouldBe(branch.Name);
        payload.Branch.Role.ShouldBe(branchUser.Role);
    }

    [Fact]
    public async Task ListUsers_ShouldReturnOkWithSeededMemberships()
    {
        var (admin, branch, adminMembership, token) = await SeedAdminMembershipAsync("List");
        var otherUser = await factory.SeedUserAsync();
        var otherMembership = await factory.SeedBranchUserAsync(otherUser.Id, branch.Id, Role.Member);

        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/branch/users");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpResponse = await _client.SendAsync(httpRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.Content.ReadFromJsonAsync<ResponseListBranchUsersJson>();
        payload.ShouldNotBeNull();
        payload!.BranchUsers.ShouldContain(entry =>
            entry.Id == adminMembership.Id &&
            entry.UserId == admin.Id &&
            entry.Role == Role.Admin &&
            entry.Active);
        payload.BranchUsers.ShouldContain(entry =>
            entry.Id == otherMembership.Id &&
            entry.UserId == otherUser.Id &&
            entry.Role == Role.Member &&
            entry.Active);
    }

    [Fact]
    public async Task AddUser_ShouldReturnOkWithNewMembership_WhenAdminAddsMember()
    {
        var (_, branch, _, token) = await SeedAdminMembershipAsync("Add");
        var targetUser = await factory.SeedUserAsync();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Member)
            .Build();

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/branch/users")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpResponse = await _client.SendAsync(httpRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.Content.ReadFromJsonAsync<ResponseAddBranchUserJson>();
        payload.ShouldNotBeNull();
        payload.BranchUser.ShouldNotBeNull();
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Email.ShouldBe(targetUser.Email);
        payload.BranchUser.Name.ShouldBe(targetUser.Name);
        payload.BranchUser.Role.ShouldBe(Role.Member);
        payload.BranchUser.Active.ShouldBeTrue();

        var persisted = await factory.ReloadBranchUserAsync(payload.BranchUser.Id);
        persisted.ShouldNotBeNull();
        persisted!.BranchId.ShouldBe(branch.Id);
        persisted.Role.ShouldBe(Role.Member);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateUserRole_ShouldReturnOkAndPersistNewRole_WhenAdminPromotesMember()
    {
        var (_, branch, _, token) = await SeedAdminMembershipAsync("UpdateRole");
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"/branch/users/{membership.Id}/role")
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpResponse = await _client.SendAsync(httpRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.Content.ReadFromJsonAsync<ResponseUpdateBranchUserRoleJson>();
        payload.ShouldNotBeNull();
        payload!.BranchUser.ShouldNotBeNull();
        payload.BranchUser.Id.ShouldBe(membership.Id);
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Role.ShouldBe(Role.Manager);
        payload.BranchUser.Active.ShouldBeTrue();

        var persisted = await factory.ReloadBranchUserAsync(membership.Id);
        persisted.ShouldNotBeNull();
        persisted.Role.ShouldBe(Role.Manager);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveUser_ShouldReturnOkAndSoftDeleteMembership_WhenAdminRemovesMember()
    {
        var (_, branch, _, token) = await SeedAdminMembershipAsync("Remove");
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);

        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/branch/users/{membership.Id}");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var httpResponse = await _client.SendAsync(httpRequest);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.Content.ReadFromJsonAsync<ResponseRemoveBranchUserJson>();
        payload.ShouldNotBeNull();
        payload!.BranchUser.ShouldNotBeNull();
        payload.BranchUser.Id.ShouldBe(membership.Id);
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Active.ShouldBeFalse();

        var persisted = await factory.ReloadBranchUserAsync(membership.Id);
        persisted.ShouldNotBeNull();
        persisted!.Active.ShouldBeFalse();
    }

    private async Task<(User Admin, Branch Branch, BranchUser Membership, string Token)> SeedAdminMembershipAsync(string label)
    {
        var admin = await factory.SeedUserAsync();
        var branch = await factory.SeedBranchAsync($"{label} {Guid.NewGuid():N}");
        var membership = await factory.SeedBranchUserAsync(admin.Id, branch.Id, Role.Admin);
        var token = factory.IssueBranchToken(membership);
        return (admin, branch, membership, token);
    }
}
