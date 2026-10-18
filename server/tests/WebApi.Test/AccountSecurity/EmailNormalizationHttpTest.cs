using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

[Collection(ServerApiCollection.Name)]
public sealed class EmailNormalizationHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task DatabaseCanonicalEmail_ShouldAgreeForRegistrationLoginDuplicateCheckAndRecoveryThrottle()
    {
        // .NET invariant lower retains U+0130; PostgreSQL's locale maps it to i. Never mix the two identity keys.
        var raw = $"\u0130DENTITY-{Guid.NewGuid():N}@EXAMPLE.INVALID";
        var canonical = await factory.ReadDb(db => db.Database.SqlQuery<string>($"SELECT lower({raw.Trim()}) AS \"Value\"").SingleAsync());
        using var registered = await _client.PostAsJsonAsync("/user/register", new RequestUserRegisterJson
        { Email = raw, Name = "Pessoa", Password = TestSeeder.DefaultPassword });
        registered.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await registered.ReadContentAsync<ResponseUserRegisterJson>()).Email.ShouldBe(canonical);
        using var login = await _client.PostAsJsonAsync("/user/login", new RequestUserLoginJson { Email = raw, Password = TestSeeder.DefaultPassword });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await login.ReadContentAsync<ResponseUserLoginJson>();
        user.Email.ShouldBe(canonical);
        using var duplicate = await _client.PostAsJsonAsync("/user/register", new RequestUserRegisterJson
        { Email = canonical, Name = "Outra pessoa", Password = TestSeeder.DefaultPassword });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        foreach (var email in new[] { raw, canonical })
        {
            using var forgot = await _client.PostAsJsonAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = email });
            forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }
        (await factory.SecurityMail.WaitToken(canonical)).Length.ShouldBe(43);
        factory.SecurityMail.Messages.Count(m => m.Recipient == canonical).ShouldBe(1);
        (await factory.ReadDb(db => db.PasswordResetTokens.CountAsync(t => t.UserId == user.UserId))).ShouldBe(1);
    }

    [Fact]
    public async Task InvitationForAUnicodeCaseAlias_ShouldMatchOnlyTheCanonicalExistingIdentity()
    {
        var (_, branch, _, admin) = await factory.SeedFullBranchContextAsync("M8EmailAlias");
        var raw = $"\u0130NVITED-{Guid.NewGuid():N}@EXAMPLE.INVALID";
        var user = await factory.SeedUserAsync(raw);
        using var issued = await _client.PostAuthAsync("/invitation", new RequestIssueInvitationJson { Email = raw, Role = Role.Member }, admin);
        issued.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await issued.ReadContentAsync<ResponseInvitationJson>()).Email.ShouldBe(user.Email);
        var token = await factory.SecurityMail.WaitToken(user.Email);
        using var accept = await _client.PostAuthAsync("/invitation/accept", new RequestInvitationTokenJson { Token = token }, factory.IssueGlobalToken(user));
        accept.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await accept.ReadContentAsync<ResponseAcceptInvitationJson>()).BranchId.ShouldBe(branch.Id);
    }
}
