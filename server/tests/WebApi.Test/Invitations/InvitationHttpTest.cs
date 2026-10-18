using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Invitations;

[Collection(ServerApiCollection.Name)]
public sealed class InvitationHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task NewUser_ShouldRegisterJoinAndPrelinkAtomically_AndPreserveAssignments()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8InviteNew");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id);
        var assignment = await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var email = $"invite-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email, operatorId: op.Id);
        var stored = (await factory.ReloadAsync<Invitation>(invite.Id))!;
        stored.Email.ShouldBe(email);
        stored.TokenHash.ShouldBe(SecurityRules.Hash(token));
        stored.ExpiresAt.ShouldBe(stored.CreatedAt.AddHours(72));
        using var preview = await _client.PostAsJsonAsync("/invitation/preview", new { token });
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var previewJson = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        previewJson.RootElement.TryGetProperty("email", out _).ShouldBeFalse();
        previewJson.RootElement.TryGetProperty("tokenHash", out _).ShouldBeFalse();
        using var register = await Register(token);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var login = await register.ReadContentAsync<ResponseUserLoginJson>();
        login.Email.ShouldBe(email);
        var membership = await factory.ReadDb(db => db.BranchUsers.SingleAsync(m => m.UserId == login.UserId && m.BranchId == branch.Id));
        membership.Active.ShouldBeTrue();
        membership.Role.ShouldBe(Role.Member);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldBe(login.UserId);
        (await factory.ReloadAsync<OperatorAccount>(assignment.Id))!.Active.ShouldBeTrue();
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Accepted);
        using var branches = await _client.GetAuthAsync("/branch/my-branches", login.ResponseToken.Token);
        branches.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var replay = await Register(token);
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ExistingAccount_ShouldRequireCorrectAuthenticatedEmail_AndNeverAllowPasswordTakeover()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8InviteExisting");
        var user = await factory.SeedUserAsync();
        var (_, token) = await factory.Invite(_client, admin, user.Email, Role.Manager);
        using var takeover = await Register(token);
        takeover.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var wrong = await factory.SeedUserAsync();
        using var mismatch = await _client.PostAuthAsync("/invitation/accept", new { token }, factory.IssueGlobalToken(wrong));
        mismatch.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var accepted = await _client.PostAuthAsync("/invitation/accept", new
        { token, role = "Admin", branchId = Guid.NewGuid(), email = wrong.Email }, factory.IssueGlobalToken(user));
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joined = await accepted.ReadContentAsync<ResponseAcceptInvitationJson>();
        joined.BranchId.ShouldBe(branch.Id);
        joined.Role.ShouldBe(Role.Manager);
        await factory.Login(_client, user); // Original password still works.
        using var replay = await _client.PostAuthAsync("/invitation/accept", new { token }, factory.IssueGlobalToken(user));
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task IssuanceAndLists_ShouldFollowRoleMatrixAndTenantScope()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8InviteRoles");
        var managerUser = await factory.SeedUserAsync();
        var managerMembership = await factory.SeedBranchUserAsync(managerUser.Id, branch.Id, Role.Manager);
        var manager = factory.IssueBranchToken(managerMembership);
        var memberUser = await factory.SeedUserAsync();
        var memberMembership = await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member);
        var member = factory.IssueBranchToken(memberMembership);
        var adminEmail = $"admin-{Guid.NewGuid():N}@example.invalid";
        var (adminInvitation, _) = await factory.Invite(_client, admin, adminEmail, Role.Admin);
        foreach (var actor in new[] { manager, member })
        {
            using var issue = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson
            { Email = $"denied-{Guid.NewGuid():N}@example.invalid", Role = Role.Admin }, actor);
            issue.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        }
        using var managerList = await _client.GetAuthAsync("/invitation", manager);
        (await managerList.ReadContentAsync<ResponseListInvitationsJson>()).Items.ShouldBeEmpty();
        using var adminList = await _client.GetAuthAsync("/invitation", admin);
        (await adminList.ReadContentAsync<ResponseListInvitationsJson>()).Items.Single().Id.ShouldBe(adminInvitation.Id);
        using var memberList = await _client.GetAuthAsync("/invitation", member);
        memberList.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var managerReissue = await _client.PostAuthAsync($"/invitation/{adminInvitation.Id}/reissue", manager);
        managerReissue.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var (_, _, _, other) = await factory.SeedFullBranchContextAsync("M8InviteOther");
        using var foreign = await _client.PostAuthAsync($"/invitation/{adminInvitation.Id}/reissue", other);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        using var invalidPrelink = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson
        { Email = $"role-{Guid.NewGuid():N}@example.invalid", Role = Role.Manager, OperatorId = Guid.NewGuid() }, admin);
        invalidPrelink.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reissue_ShouldInvalidateOldCapability_DeclineIsIdempotent_AndExpirationIsEffectiveOnReads()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8Lifecycle");
        var email = $"lifecycle-{Guid.NewGuid():N}@example.invalid";
        var (invite, old) = await factory.Invite(_client, admin, email);
        using var declined = await _client.PostAsJsonAsync("/invitation/decline", new { token = old });
        declined.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var declineAgain = await _client.PostAsJsonAsync("/invitation/decline", new { token = old });
        declineAgain.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var declinedAccept = await Register(old);
        declinedAccept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var reissue = await _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", admin);
        reissue.StatusCode.ShouldBe(HttpStatusCode.OK);
        var current = await factory.SecurityMail.WaitToken(email, 1);
        current.ShouldNotBe(old);
        using var oldPreview = await _client.PostAsJsonAsync("/invitation/preview", new { token = old });
        oldPreview.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var oldAccept = await Register(old);
        oldAccept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await factory.UpdateDb(db => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Invitations\" SET \"ExpiresAt\" = now() - interval '1 second' WHERE \"Id\" = {invite.Id}"));
        using var list = await _client.GetAuthAsync("/invitation?status=Expired", admin);
        (await list.ReadContentAsync<ResponseListInvitationsJson>()).Items.Single().Status.ShouldBe(InvitationStatus.Expired);
        using var expired = await Register(current);
        expired.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Expired pending rows release their unique slot within the issuance transaction.
        using var replacement = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = email, Role = Role.Member }, admin);
        replacement.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Expired);
    }

    [Fact]
    public async Task NormalizedEmail_ShouldPreventDuplicatePendingInvitesAndAccounts()
    {
        var (_, _, _, admin) = await factory.SeedFullBranchContextAsync("M8Email");
        var email = $"normalize-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email);
        using var repeat = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = email.ToUpperInvariant(), Role = Role.Member }, admin);
        repeat.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var accepted = await _client.PostAsJsonAsync("/invitation/register", new
        { token, name = "Pessoa", password = "Password123", email = "forged@example.invalid", branchId = Guid.NewGuid(), role = "Admin" });
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
        var login = await accepted.ReadContentAsync<ResponseUserLoginJson>();
        login.Email.ShouldBe(email);
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Role.ShouldBe(Role.Member);
    }

    [Fact]
    public async Task InactiveAccount_ShouldNeverBeRecreatedOrAccepted()
    {
        var (_, _, _, admin) = await factory.SeedFullBranchContextAsync("M8InactiveInvite");
        var user = await factory.SeedUserAsync();
        var (_, token) = await factory.Invite(_client, admin, user.Email);
        var identity = factory.IssueGlobalToken(user);
        await factory.UpdateDb(async db => (await db.Users.FindAsync(user.Id))!.Active = false);
        using var register = await Register(token);
        register.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var accept = await _client.PostAuthAsync("/invitation/accept", new { token }, identity);
        accept.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task InactiveMembership_ShouldReactivateWithInvitedRole_WithoutOverwritingActiveMembership()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8Reactivate");
        var user = await factory.SeedUserAsync();
        var previous = await factory.SeedBranchUserAsync(user.Id, branch.Id, Role.Manager);
        await factory.UpdateDb(async db => (await db.BranchUsers.FindAsync(previous.Id))!.Active = false);
        var (_, token) = await factory.Invite(_client, admin, user.Email, Role.Member);
        using var accept = await _client.PostAuthAsync("/invitation/accept", new { token }, factory.IssueGlobalToken(user));
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        var joined = await accept.ReadContentAsync<ResponseAcceptInvitationJson>();
        joined.BranchUserId.ShouldBe(previous.Id);
        (await factory.ReloadAsync<BranchUser>(previous.Id))!.Role.ShouldBe(Role.Member);
        using var activeConflict = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = user.Email, Role = Role.Admin }, admin);
        activeConflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SimultaneousAccept_ShouldCreateExactlyOneUserMembershipAndLink()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8AcceptRace");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var email = $"race-{Guid.NewGuid():N}@example.invalid";
        var (invitation, token) = await factory.Invite(_client, admin, email, operatorId: op.Id);
        await using var held = await factory.HoldSecurity(SecurityRules.BranchKey(branch.Id), SecurityRules.EmailKey(email));
        var first = Register(token);
        await held.WaitForWaitersAsync(1);
        var second = Register(token);
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == email))).ShouldBe(1);
        var userId = (await r1.ReadContentAsync<ResponseUserLoginJson>()).UserId;
        (await factory.ReadDb(db => db.BranchUsers.CountAsync(m => m.UserId == userId && m.BranchId == branch.Id))).ShouldBe(1);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldBe(userId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prelink_ShouldRefuseChangedOrRetiredOperator_WithoutPartialRegistration(bool retired)
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8StaleOperator");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var email = $"stale-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email, operatorId: op.Id);
        var existing = await factory.SeedUserAsync();
        await factory.SeedBranchUserAsync(existing.Id, branch.Id, Role.Member);
        await factory.UpdateDb(async db =>
        {
            var current = (await db.Operators.FindAsync(op.Id))!;
            if (retired) current.Active = false; else current.UserId = existing.Id;
        });
        using var accept = await Register(token);
        accept.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == email))).ShouldBe(0);
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public async Task TwoInvitationsCompetingForOperator_ShouldHaveOneAtomicWinner()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8CompetingInvite");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var firstEmail = $"winner-{Guid.NewGuid():N}@example.invalid";
        var secondEmail = $"loser-{Guid.NewGuid():N}@example.invalid";
        var (_, firstToken) = await factory.Invite(_client, admin, firstEmail, operatorId: op.Id);
        var (_, secondToken) = await factory.Invite(_client, admin, secondEmail, operatorId: op.Id);
        var responses = await Task.WhenAll(Register(firstToken), Register(secondToken));
        responses.Select(r => r.StatusCode).Order().ShouldBe(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }.Order());
        foreach (var response in responses) response.Dispose();
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == firstEmail || u.Email == secondEmail))).ShouldBe(1);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeliveryFailure_ShouldPersistTruthfulFailure_AndReissueCanRecover()
    {
        var (_, _, _, admin) = await factory.SeedFullBranchContextAsync("M8DeliveryFail");
        var email = $"failed-{Guid.NewGuid():N}@example.invalid";
        factory.SecurityMail.FailRecipients.TryAdd(email, true);
        using var issue = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = email, Role = Role.Member }, admin);
        issue.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invite = await issue.ReadContentAsync<ResponseInvitationJson>();
        invite.DeliveryStatus.ShouldBe(MailDeliveryStatus.Failed);
        (await factory.ReloadAsync<Invitation>(invite.Id))!.DeliveryStatus.ShouldBe(MailDeliveryStatus.Failed);
        factory.SecurityMail.FailRecipients.TryRemove(email, out _);
        using var reissue = await _client.PostAuthAsync($"/invitation/{invite.Id}/reissue", admin);
        (await reissue.ReadContentAsync<ResponseInvitationJson>()).DeliveryStatus.ShouldBe(MailDeliveryStatus.Sent);
        (await factory.SecurityMail.WaitToken(email)).Length.ShouldBe(43);
    }

    private Task<HttpResponseMessage> Register(string token) => _client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
    { Token = token, Name = "Pessoa convidada", Password = TestSeeder.DefaultPassword });
}
