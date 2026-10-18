using CommonTestUtilities.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Domain.Interfaces;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Invitations;

[Collection(ServerApiCollection.Name)]
public sealed class InvitationRollbackHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FailureAtInviteConsumption_ShouldRollbackNewUserMembershipPrelinkAndRefresh()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8InviteRollback");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var email = $"rollback-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email, operatorId: op.Id);
        await using (var failure = await M8WriteFailure.Install(factory, "Invitations", "Id", invite.Id))
        {
            using var failed = await Register(token);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == email))).ShouldBe(0);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldBeNull();
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Pending);
        using var retry = await Register(token);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = await retry.ReadContentAsync<ResponseUserLoginJson>();
        (await factory.ReadDb(db => db.BranchUsers.CountAsync(m => m.UserId == user.UserId && m.BranchId == branch.Id))).ShouldBe(1);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.UserId && t.Active))).ShouldBe(1);
    }

    [Fact]
    public async Task FailedIssueCommit_ShouldNeverSendCapabilityMail()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8IssueRollback");
        var email = $"issue-rollback-{Guid.NewGuid():N}@example.invalid";
        await using (var failure = await M8WriteFailure.Install(factory, "Invitations", "BranchId", branch.Id))
        {
            using var failed = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = email, Role = Role.Member }, admin);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        factory.SecurityMail.Messages.Any(m => m.Recipient == email).ShouldBeFalse();
        (await factory.ReadDb(db => db.Invitations.CountAsync(i => i.Email == email))).ShouldBe(0);
    }

    [Fact]
    public async Task TokenGenerationFailure_ShouldRollbackInvitationRegistrationBeforeCommit()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8TokenRollback");
        var op = await factory.SeedOperatorAsync(branch.Id);
        var email = $"token-rollback-{Guid.NewGuid():N}@example.invalid";
        var (invite, token) = await factory.Invite(_client, admin, email, operatorId: op.Id);
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITokenServices>();
            services.AddSingleton(new TokenServicesBuilder().FailGlobalTokenGeneration().Build());
        }));
        using var client = host.CreateClient();
        using var failed = await client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = token, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
        failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await factory.ReadDb(db => db.Users.CountAsync(u => u.Email == email))).ShouldBe(0);
        (await factory.ReloadAsync<Operator>(op.Id))!.UserId.ShouldBeNull();
        (await factory.ReloadAsync<Invitation>(invite.Id))!.Status.ShouldBe(InvitationStatus.Pending);
    }

    private Task<HttpResponseMessage> Register(string token) => _client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
    { Token = token, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
}
