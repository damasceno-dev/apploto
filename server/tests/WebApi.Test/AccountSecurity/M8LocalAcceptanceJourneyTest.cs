using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

/// <summary>The backend acceptance journey uses real HTTP, PostgreSQL and captured local SMTP, never fake mail.</summary>
[Collection(ServerApiCollection.Name)]
public sealed class M8LocalAcceptanceJourneyTest(ServerWebApplicationFactory factory) : IAsyncLifetime
{
    private readonly LocalMailpitCapture _capture = new();
    public Task InitializeAsync() => _capture.StartAsync();
    public Task DisposeAsync() => _capture.DisposeAsync().AsTask();

    [Fact]
    public async Task AccountRecoveryRequestResolutionAndBothInviteAcceptPaths_ShouldWorkThroughRealLocalMail()
    {
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISecurityMailSender>();
            services.AddSingleton<ISecurityMailSender>(new SmtpSecurityMailSender(new SecurityMailOptions(true, "127.0.0.1",
                _capture.GetMappedPublicPort(1025), "no-reply@loto.invalid", true, null, null)));
        }));
        using var client = host.CreateClient();
        host.Services.GetRequiredService<ISecurityMailSender>().ShouldBeOfType<SmtpSecurityMailSender>();
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8LocalJourney");
        var email = $"journey-{Guid.NewGuid():N}@example.invalid";
        await factory.SeedUserAsync(email, "Pessoa local");
        var login = await Login(client, email, TestSeeder.DefaultPassword);
        using var profile = await client.PutAuthAsync("/user/profile", new RequestUpdateProfileJson { Name = "Nome local atualizado" }, login.ResponseToken.Token);
        (await profile.ReadContentAsync<ResponseUserProfileJson>()).Name.ShouldBe("Nome local atualizado");
        using var change = await client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
        { CurrentPassword = TestSeeder.DefaultPassword, NewPassword = "ChangedPassword123" }, login.ResponseToken.Token);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var oldRenew = await client.PostAsJsonAsync("/user/renew-token", new RequestUserRenewTokenJson { Value = login.ResponseToken.RefreshToken });
        oldRenew.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        login = await Login(client, email, "ChangedPassword123");
        using var logout = await client.PostAuthAsync("/user/logout", new RequestLogoutJson { RefreshToken = login.ResponseToken.RefreshToken }, login.ResponseToken.Token);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        using var forgot = await client.PostAsJsonAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = email });
        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var resetCapability = await _capture.WaitToken(email);
        using var reset = await client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson { Token = resetCapability, NewPassword = "ResetPassword123" });
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        login = await Login(client, email, "ResetPassword123");
        await factory.SeedBranchUserAsync(login.UserId, branch.Id, Role.Member);
        var branchSession = await Session(client, branch.Id, login.ResponseToken.Token);
        using var requested = await client.PostAuthAsync("/operator-link-request", branchSession);
        var linkRequest = await requested.ReadContentAsync<ResponseOperatorLinkRequestJson>();
        using var createdOperator = await client.PostAuthAsync("/operator", new RequestCreateOperatorJson { Name = "Operador local", UserId = login.UserId }, admin);
        var op = await createdOperator.ReadContentAsync<ResponseCreateOperatorJson>();
        using var resolved = await client.PostAuthAsync($"/operator-link-request/{linkRequest.Id}/resolve", admin);
        (await resolved.ReadContentAsync<ResponseOperatorLinkRequestJson>()).HasUsableAccount.ShouldBeFalse();
        var account = await factory.SeedAccountAsync(branch.Id);
        using var assigned = await client.PostAuthAsync($"/operator/{op.Id}/accounts", new RequestAssignAccountJson { AccountId = account.Id }, admin);
        assigned.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var context = await client.GetAuthAsync("/operator/self-context", branchSession);
        (await context.ReadContentAsync<ResponseSelfContextJson>()).AvailableAccounts.Single().AccountId.ShouldBe(account.Id);

        // New user invitation: real captured mail -> atomic registration, membership and pre-link.
        using var unlinked = await client.PostAuthAsync("/operator", new RequestCreateOperatorJson { Name = "Novo operador local" }, admin);
        var prelink = await unlinked.ReadContentAsync<ResponseCreateOperatorJson>();
        var newEmail = $"journey-new-{Guid.NewGuid():N}@example.invalid";
        using var issued = await client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = newEmail, Role = Role.Member, OperatorId = prelink.Id }, admin);
        (await issued.ReadContentAsync<ResponseInvitationJson>()).DeliveryStatus.ShouldBe(MailDeliveryStatus.Sent);
        var capability = await _capture.WaitToken(newEmail);
        using var registered = await client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = capability, Name = "Pessoa convidada local", Password = TestSeeder.DefaultPassword });
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invited = await registered.ReadContentAsync<ResponseUserLoginJson>();
        var invitedSession = await Session(client, branch.Id, invited.ResponseToken.Token);
        using var invitedContext = await client.GetAuthAsync("/operator/self-context", invitedSession);
        var invitedState = await invitedContext.ReadContentAsync<ResponseSelfContextJson>();
        invitedState.OperatorId.ShouldBe(prelink.Id);
        invitedState.AvailableAccounts.ShouldBeEmpty(); // Pre-link is never an implicit account grant.

        // Existing password account: captured capability requires its matching identity, without replacing password.
        var existing = await factory.SeedUserAsync();
        using var existingIssue = await client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = existing.Email, Role = Role.Manager }, admin);
        existingIssue.StatusCode.ShouldBe(HttpStatusCode.Created);
        var existingCapability = await _capture.WaitToken(existing.Email);
        var existingLogin = await Login(client, existing.Email, TestSeeder.DefaultPassword);
        using var accepted = await client.PostAuthAsync("/invitation/accept", new RequestInvitationTokenJson { Token = existingCapability }, existingLogin.ResponseToken.Token);
        (await accepted.ReadContentAsync<ResponseAcceptInvitationJson>()).Role.ShouldBe(Role.Manager);
    }

    private static async Task<ResponseUserLoginJson> Login(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsJsonAsync("/user/login", new RequestUserLoginJson { Email = email, Password = password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseUserLoginJson>();
    }
    private static async Task<string> Session(HttpClient client, Guid branchId, string identity)
    {
        using var response = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = branchId }, identity);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.ReadContentAsync<ResponseCreateBranchSessionJson>()).Token;
    }
}
