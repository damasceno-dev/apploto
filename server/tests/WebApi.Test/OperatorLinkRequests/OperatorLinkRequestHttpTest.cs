using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.OperatorLinkRequests;

[Collection(ServerApiCollection.Name)]
public sealed class OperatorLinkRequestHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MemberRequest_ShouldRepeatDeterministically_AndResolveWithoutGrantingAnAccount()
    {
        var (_, branch, _, adminToken) = await factory.SeedFullBranchContextAsync("M8Request");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        var memberToken = factory.IssueBranchToken(membership);
        using var first = await _client.PostAuthAsync("/operator-link-request", memberToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = await first.ReadContentAsync<ResponseOperatorLinkRequestJson>();
        request.UserId.ShouldBe(user.Id);
        request.OperatorId.ShouldBeNull();
        request.HasUsableAccount.ShouldBeFalse();
        using var repeat = await _client.PostAuthAsync("/operator-link-request", memberToken);
        (await repeat.ReadContentAsync<ResponseOperatorLinkRequestJson>()).Id.ShouldBe(request.Id);
        using var list = await _client.GetAuthAsync("/operator-link-request?status=Pending&pageSize=1", adminToken);
        var page = await list.ReadContentAsync<ResponseListOperatorLinkRequestsJson>();
        page.TotalCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(request.Id);
        using var missingSetup = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", adminToken);
        missingSetup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var createOperator = await _client.PostAuthAsync("/operator", new RequestCreateOperatorJson { Name = "Operador", UserId = user.Id }, adminToken);
        createOperator.StatusCode.ShouldBe(HttpStatusCode.Created);
        var op = await createOperator.ReadContentAsync<ResponseCreateOperatorJson>();
        using var resolve = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", adminToken);
        resolve.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resolved = await resolve.ReadContentAsync<ResponseOperatorLinkRequestJson>();
        resolved.Status.ShouldBe(OperatorLinkRequestStatus.Resolved);
        resolved.OperatorId.ShouldBe(op.Id);
        resolved.HasUsableAccount.ShouldBeFalse();
        resolved.ResolvedAt.ShouldNotBeNull();
        (await factory.ReadDb(db => db.OperatorAccounts.CountAsync(a => a.OperatorId == op.Id))).ShouldBe(0);
        using var own = await _client.GetAuthAsync("/operator-link-request/self", memberToken);
        (await own.ReadContentAsync<ResponseOwnOperatorLinkRequestJson>()).Request!.Id.ShouldBe(request.Id);
        using var repeatedResolution = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", adminToken);
        (await repeatedResolution.ReadContentAsync<ResponseOperatorLinkRequestJson>()).ResolvedAt.ShouldBe(resolved.ResolvedAt);
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, true, true)]
    public async Task Eligibility_ShouldUseActiveOperatorAndUsableAccount(bool operatorActive, bool assignmentActive, bool accountActive)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M8Eligibility", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id, active: operatorActive);
        var account = await factory.SeedAccountAsync(branch.Id, active: accountActive);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id, active: assignmentActive);
        using var response = await _client.PostAuthAsync("/operator-link-request", token);
        response.StatusCode.ShouldBe(operatorActive && assignmentActive && accountActive ? HttpStatusCode.Conflict : HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(AccountType.BankAccount)]
    [InlineData(AccountType.Tab)]
    public async Task NonTerminalGrants_ShouldNotMakeCockpitSetupUsable(AccountType type)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("M8NonTerminal", Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, type);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        using var response = await _client.PostAuthAsync("/operator-link-request", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadContentAsync<ResponseOperatorLinkRequestJson>()).HasUsableAccount.ShouldBeFalse();
    }

    [Fact]
    public async Task Requests_ShouldProtectRolesTenantAndRemovedMembership_AndQueueCountHasNoDateBound()
    {
        var (_, branch, _, adminToken) = await factory.SeedFullBranchContextAsync("M8Visibility");
        await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        var member = factory.IssueBranchToken(membership);
        using var create = await _client.PostAuthAsync("/operator-link-request", member);
        var request = await create.ReadContentAsync<ResponseOperatorLinkRequestJson>();
        using var memberList = await _client.GetAuthAsync("/operator-link-request", member);
        memberList.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var memberResolve = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", member);
        memberResolve.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var adminCreate = await _client.PostAuthAsync("/operator-link-request", adminToken);
        adminCreate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var (_, _, _, other) = await factory.SeedFullBranchContextAsync("M8OtherBranch");
        using var otherResolve = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", other);
        otherResolve.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        foreach (var day in new[] { DateTime.Today.AddDays(-1), DateTime.Today, DateTime.Today.AddDays(1) })
        {
            using var queue = await _client.GetAuthAsync($"/report/manager-queue?date={day:yyyy-MM-dd}", adminToken);
            queue.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await queue.ReadContentAsync<ResponseManagerQueueJson>()).PendingOperatorLinkRequestCount.ShouldBe(1);
        }
        using var remove = await _client.DeleteAuthAsync($"/branch/users/{membership.Id}", adminToken);
        remove.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var list = await _client.GetAuthAsync("/operator-link-request", adminToken);
        (await list.ReadContentAsync<ResponseListOperatorLinkRequestsJson>()).TotalCount.ShouldBe(0);
        using var stale = await _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", adminToken);
        stale.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await factory.ReloadAsync<OperatorLinkRequest>(request.Id))!.Status.ShouldBe(OperatorLinkRequestStatus.Pending);
    }

    [Fact]
    public async Task ConcurrentRequestsAndManagers_ShouldPersistOnePendingAndResolveOnce()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8RequestRace");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        var member = factory.IssueBranchToken(membership);
        await using (var held = await factory.HoldSecurity(SecurityRules.BranchKey(branch.Id)))
        {
            var first = _client.PostAuthAsync("/operator-link-request", member);
            await held.WaitForWaitersAsync(1);
            var second = _client.PostAuthAsync("/operator-link-request", member);
            await held.WaitForWaitersAsync(2);
            await held.ReleaseAsync();
            using var r1 = await first;
            using var r2 = await second;
            (await r1.ReadContentAsync<ResponseOperatorLinkRequestJson>()).Id.ShouldBe((await r2.ReadContentAsync<ResponseOperatorLinkRequestJson>()).Id);
        }
        var request = await factory.ReadDb(db => db.OperatorLinkRequests.SingleAsync(r => r.UserId == user.Id));
        await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var responses = await Task.WhenAll(_client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", admin),
            _client.PostAuthAsync($"/operator-link-request/{request.Id}/resolve", admin));
        foreach (var response in responses) { response.StatusCode.ShouldBe(HttpStatusCode.OK); response.Dispose(); }
        (await factory.ReloadAsync<OperatorLinkRequest>(request.Id))!.Status.ShouldBe(OperatorLinkRequestStatus.Resolved);
    }

    [Fact]
    public async Task PendingUniqueIndex_ShouldRejectBypassingApplicationCoordination()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("M8RequestConstraint", Role.Member);
        await factory.UpdateDb(async db => await db.OperatorLinkRequests.AddAsync(new OperatorLinkRequest { BranchId = branch.Id, UserId = user.Id }));
        await Should.ThrowAsync<DbUpdateException>(() => factory.UpdateDb(async db =>
            await db.OperatorLinkRequests.AddAsync(new OperatorLinkRequest { BranchId = branch.Id, UserId = user.Id })));
    }
}
