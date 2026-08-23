using System.Net;
using System.Text.Json;
using CommonTestUtilities.Requests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Transactions;
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
        var (_, branch, branchUser, token) = await factory.SeedFullBranchContextAsync("Current");

        var httpResponse = await _client.GetAuthAsync("/branch/current", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseGetCurrentBranchSummaryJson>();
        payload.Branch.ShouldNotBeNull();
        payload.Branch.Id.ShouldBe(branch.Id);
        payload.Branch.Name.ShouldBe(branch.Name);
        payload.Branch.Role.ShouldBe(branchUser.Role);
    }

    [Fact]
    public async Task GetCurrent_ShouldReturnServerAuthoritativeBranchLocalDateAndTime()
    {
        var fixedLocalNow = new DateTime(2026, 8, 19, 23, 47, 12, DateTimeKind.Unspecified);
        await using var customFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IBranchClock>();
            services.AddSingleton<IBranchClock>(new FixedBranchClock(fixedLocalNow));
        }));
        using var client = customFactory.CreateClient();
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CurrentClock");

        var httpResponse = await client.GetAuthAsync("/branch/current", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rawJson = await httpResponse.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(rawJson);
        var root = document.RootElement;
        root.GetProperty("branchLocalDate").GetString().ShouldBe("2026-08-19");
        root.GetProperty("branchLocalDateTime").GetString().ShouldBe("2026-08-19T23:47:12");

        var payload = JsonSerializer.Deserialize<ResponseGetCurrentBranchSummaryJson>(
            rawJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        payload.ShouldNotBeNull();
        payload.BranchLocalDate.ShouldBe(new DateOnly(2026, 8, 19));
        payload.BranchLocalDateTime.ShouldBe(fixedLocalNow);
        payload.BranchLocalDateTime.Kind.ShouldBe(DateTimeKind.Unspecified);
    }

    [Fact]
    public async Task ListUsers_ShouldReturnOkWithSeededMemberships()
    {
        var (admin, branch, adminMembership, token) = await factory.SeedFullBranchContextAsync("List");
        var otherUser = await factory.SeedUserAsync();
        var otherMembership = await factory.SeedBranchUserAsync(otherUser.Id, branch.Id, Role.Member);

        var httpResponse = await _client.GetAuthAsync("/branch/users", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListBranchUsersJson>();
        payload.BranchUsers.ShouldContain(entry =>
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
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("Add");
        var targetUser = await factory.SeedUserAsync();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Member)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/users", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseAddBranchUserJson>();
        payload.BranchUser.ShouldNotBeNull();
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Email.ShouldBe(targetUser.Email);
        payload.BranchUser.Name.ShouldBe(targetUser.Name);
        payload.BranchUser.Role.ShouldBe(Role.Member);
        payload.BranchUser.Active.ShouldBeTrue();

        var persisted = await factory.ReloadAsync<BranchUser>(payload.BranchUser.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Role.ShouldBe(Role.Member);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateUserRole_ShouldReturnOkAndPersistNewRole_WhenAdminPromotesMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("UpdateRole");
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/branch/users/{membership.Id}/role", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseUpdateBranchUserRoleJson>();
        payload.BranchUser.ShouldNotBeNull();
        payload.BranchUser.Id.ShouldBe(membership.Id);
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Role.ShouldBe(Role.Manager);
        payload.BranchUser.Active.ShouldBeTrue();

        var persisted = await factory.ReloadAsync<BranchUser>(membership.Id);
        persisted.ShouldNotBeNull();
        persisted.Role.ShouldBe(Role.Manager);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task RemoveUser_ShouldReturnOkAndSoftDeleteMembership_WhenAdminRemovesMember()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("Remove");
        var targetUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(targetUser.Id, branch.Id, Role.Member);
        var linkedOperator = await factory.SeedOperatorAsync(branch.Id, userId: targetUser.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/branch/users/{membership.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseRemoveBranchUserJson>();
        payload.BranchUser.ShouldNotBeNull();
        payload.BranchUser.Id.ShouldBe(membership.Id);
        payload.BranchUser.UserId.ShouldBe(targetUser.Id);
        payload.BranchUser.Active.ShouldBeFalse();

        var persisted = await factory.ReloadAsync<BranchUser>(membership.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();

        var persistedOperator = await factory.ReloadAsync<Operator>(linkedOperator.Id);
        persistedOperator.ShouldNotBeNull();
        persistedOperator.UserId.ShouldBeNull();

        var operatorResponse = await _client.GetAuthAsync($"/operator/{linkedOperator.Id}", token);
        operatorResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await operatorResponse.ReadContentAsync<ResponseOperatorJson>()).UserId.ShouldBeNull();
    }

    private sealed class FixedBranchClock(DateTime localNow) : IBranchClock
    {
        public DateTime UtcNow() => new(2026, 8, 20, 2, 47, 12, DateTimeKind.Utc);
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => localNow;
        public DateTime LocalBusinessDate(DateTime utcInstant) => localNow.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant) =>
            localBusinessDate.Date == localNow.Date;
    }
}
