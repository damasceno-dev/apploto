using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

[Collection(ServerApiCollection.Name)]
public sealed class MembershipSecurityHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Manager)]
    public async Task SelfMembershipGuard_ShouldHaveDedicatedError_EvenWithOtherAdmins(Role role)
    {
        var (_, branch, own, token) = await factory.SeedFullBranchContextAsync("M8Self", role);
        var other = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(other.Id, branch.Id, Role.Admin);
        using var demote = await _client.PutAuthAsync($"/branch/users/{own.Id}/role", new RequestUpdateBranchUserRoleJson { Role = Role.Member }, token);
        using var remove = await _client.DeleteAuthAsync($"/branch/users/{own.Id}", token);
        foreach (var response in new[] { demote, remove })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.BRANCH_USER_SELF_MEMBERSHIP_CONFLICT]);
        }
        (await factory.ReloadAsync<BranchUser>(own.Id))!.Role.ShouldBe(role);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MutualAdminChanges_ShouldRecheckQueuedActor_AndRetainAnActiveAdmin(bool remove)
    {
        var (_, branch, firstAdmin, firstToken) = await factory.SeedFullBranchContextAsync("M8AdminRace");
        var user = await factory.SeedUserAsync();
        var secondAdmin = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Admin);
        var secondToken = factory.IssueBranchToken(secondAdmin);
        Task<HttpResponseMessage> Change(Guid id, string token) => remove ? _client.DeleteAuthAsync($"/branch/users/{id}", token)
            : _client.PutAuthAsync($"/branch/users/{id}/role", new RequestUpdateBranchUserRoleJson { Role = Role.Member }, token);
        await using var held = await factory.HoldSecurity(SecurityRules.BranchKey(branch.Id));
        var first = Change(secondAdmin.Id, firstToken);
        await held.WaitForWaitersAsync(1);
        var second = Change(firstAdmin.Id, secondToken);
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(remove ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden);
        (await factory.ReadDb(db => db.BranchUsers.CountAsync(m => m.BranchId == branch.Id && m.Active && m.User.Active && m.Role == Role.Admin))).ShouldBe(1);
    }

    [Fact]
    public async Task Removal_ShouldUnlinkOperator_KeepHistoryAndAssignments_AndInvalidateBranchToken()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8Unlink");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id);
        var assignment = await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var memberToken = factory.IssueBranchToken(membership);
        using var response = await _client.DeleteAuthAsync($"/branch/users/{membership.Id}", admin);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var persisted = (await factory.ReloadAsync<Operator>(op.Id))!;
        persisted.Active.ShouldBeTrue();
        persisted.UserId.ShouldBeNull();
        (await factory.ReloadAsync<OperatorAccount>(assignment.Id))!.Active.ShouldBeTrue();
        using var access = await _client.GetAuthAsync("/operator/self-context", memberToken);
        access.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
