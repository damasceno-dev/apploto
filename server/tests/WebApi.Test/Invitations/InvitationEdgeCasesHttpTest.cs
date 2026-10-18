using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Invitations;

[Collection(ServerApiCollection.Name)]
public sealed class InvitationEdgeCasesHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ExistingUserWithAnotherLink_ShouldNotConsumePrelinkInviteOrCreateMembership()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8ExistingLink");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Member);
        await factory.UpdateDb(async db => (await db.BranchUsers.FindAsync(membership.Id))!.Active = false);
        var currentOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var invitedOperator = await factory.SeedOperatorAsync(branch.Id);
        var (invite, token) = await factory.Invite(_client, admin, user.Email, operatorId: invitedOperator.Id);
        using var accept = await _client.PostAuthAsync("/invitation/accept", new { token }, factory.IssueGlobalToken(user));
        accept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReloadAsync<BranchUser>(membership.Id))!.Active.ShouldBeFalse();
        (await factory.ReloadAsync<Operator>(currentOperator.Id))!.UserId.ShouldBe(user.Id);
        (await factory.ReloadAsync<Operator>(invitedOperator.Id))!.UserId.ShouldBeNull();
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public async Task ExistingUserAcceptanceFailure_ShouldRollbackMembershipReactivationRoleAndPrelink()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8ExistingRollback");
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Manager);
        await factory.UpdateDb(async db => (await db.BranchUsers.FindAsync(membership.Id))!.Active = false);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var (invite, token) = await factory.Invite(_client, admin, user.Email, operatorId: op.Id);
        await using (var failure = await M8WriteFailure.Install(factory, "Invitations", "Id", invite.Id))
        {
            using var accept = await _client.PostAuthAsync("/invitation/accept", new { token }, factory.IssueGlobalToken(user));
            accept.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        var previous = (await factory.ReloadAsync<BranchUser>(membership.Id))!;
        previous.Active.ShouldBeFalse();
        previous.Role.ShouldBe(Role.Manager);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldBeNull();
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public async Task ReissuedOldCapability_ShouldNeverWinAnAcceptanceRace()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8ReissueRace");
        var email = $"reissue-race-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email);
        await using var held = await factory.HoldSecurity(SecurityRules.BranchKey(branch.Id), SecurityRules.EmailKey(email));
        var reissue = _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", admin);
        await held.WaitForWaitersAsync(1);
        var accept = _client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson { Token = token, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var reissued = await reissue;
        using var oldAccept = await accept;
        reissued.StatusCode.ShouldBe(HttpStatusCode.OK);
        oldAccept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == email))).ShouldBe(0);
    }

    [Fact]
    public async Task DuplicateIssueRace_ShouldLeaveOnePendingInvite()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8IssueRace");
        var email = $"issue-race-{Guid.NewGuid():N}@example.invalid";
        var body = new RequestIssueInvitationJson { Email = email, Role = Role.Member };
        var responses = await Task.WhenAll(_client.PostAuthAsync("/invitation", body, admin), _client.PostAuthAsync("/invitation", body, admin));
        responses.Select(r => r.StatusCode).Order().ShouldBe(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.Order());
        foreach (var response in responses) response.Dispose();
        (await factory.ReadDb(db => db.Invitations.CountAsync(i => i.BranchId == branch.Id && i.Email == email && i.Status == InvitationStatus.Pending))).ShouldBe(1);
    }

    [Fact]
    public async Task ExpiredDeclinedCapability_ShouldBeInvalid_AndAcceptedInviteCannotReissue()
    {
        var (_, _, _, admin) = await factory.SeedFullBranchContextAsync("M8TerminalInvites");
        var user = await factory.SeedUserAsync();
        var (invite, token) = await factory.Invite(_client, admin, user.Email);
        using var declined = await _client.PostAsJsonAsync("/invitation/decline", new { token });
        declined.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.UpdateDb(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Invitations\" SET \"ExpiresAt\" = now() - interval '1 second' WHERE \"Id\" = {invite.Id}"));
        using var expired = await _client.PostAsJsonAsync("/invitation/decline", new { token });
        expired.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var reissue = await _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", admin);
        reissue.StatusCode.ShouldBe(HttpStatusCode.OK);
        var replacement = await factory.SecurityMail.WaitToken(user.Email, 1);
        using var accepted = await _client.PostAuthAsync("/invitation/accept", new { token = replacement }, factory.IssueGlobalToken(user));
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var acceptedReissue = await _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", admin);
        acceptedReissue.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reissue_ShouldRecoverFromRetiredPreselection_ByClearingOrReplacingIt(bool replace)
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8PrelinkRecovery");
        var retired = await factory.SeedOperatorAsync(branch.Id);
        var email = $"recover-prelink-{Guid.NewGuid():N}@example.invalid";
        var (invite, oldToken) = await factory.Invite(_client, admin, email, operatorId: retired.Id);
        await factory.UpdateDb(async db => (await db.Operators.FindAsync(retired.Id))!.Active = false);
        var replacement = replace ? await factory.SeedOperatorAsync(branch.Id) : null;
        using var reissued = await _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", new RequestReissueInvitationJson { OperatorId = replacement?.Id }, admin);
        reissued.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resource = await reissued.ReadContentAsync<server.Communication.Responses.ResponseInvitationJson>();
        resource.OperatorId.ShouldBe(replacement?.Id);
        var token = await factory.SecurityMail.WaitToken(email, 1);
        using var invalidOld = await _client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = oldToken, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
        invalidOld.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var accepted = await _client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = token, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = await accepted.ReadContentAsync<server.Communication.Responses.ResponseUserLoginJson>();
        (await factory.ReadDb(db => db.Operators.CountAsync(op => op.BranchId == branch.Id && op.UserId == user.UserId && op.Active))).ShouldBe(replace ? 1 : 0);
    }
}
