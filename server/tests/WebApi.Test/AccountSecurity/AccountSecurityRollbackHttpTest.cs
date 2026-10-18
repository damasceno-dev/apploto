using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Application.Services;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

[Collection(ServerApiCollection.Name)]
public sealed class AccountSecurityRollbackHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task FailedResetCommit_ShouldRestoreTokenPasswordAndSessions_ThenQueuedRenewalSucceeds()
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        var token = await factory.RequestRecovery(_client, user);
        await using (var failure = await M8WriteFailure.Install(factory, "Users", "Id", user.Id))
        await using (var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email)))
        {
            var reset = _client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson { Token = token, NewPassword = "NewPassword123" });
            await held.WaitForWaitersAsync(1);
            var renew = _client.PostAsJsonAsync("/user/renew-token", new RequestUserRenewTokenJson { Value = login.ResponseToken.RefreshToken });
            await held.WaitForWaitersAsync(2);
            await held.ReleaseAsync();
            using var failed = await reset;
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            using var renewed = await renew;
            renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        new PasswordEncryption().VerifyPassword(TestSeeder.DefaultPassword, (await factory.ReloadAsync<User>(user.Id))!.Password).ShouldBeTrue();
        var saved = await factory.ReadDb(db => db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id));
        saved.Active.ShouldBeTrue();
        saved.ConsumedAt.ShouldBeNull();
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(1);
        using var retry = await _client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson { Token = token, NewPassword = "NewPassword123" });
        retry.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task FailedChangePasswordAndLogout_ShouldLeaveAllSessionsActive()
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        await using (var failure = await M8WriteFailure.Install(factory, "Users", "Id", user.Id))
        {
            using var failed = await _client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
            { CurrentPassword = TestSeeder.DefaultPassword, NewPassword = "NewPassword123" }, login.ResponseToken.Token);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(1);
        await using (var failure = await M8WriteFailure.Install(factory, "RefreshTokens", "UserId", user.Id))
        {
            using var failed = await _client.PostAuthAsync("/user/logout", new RequestLogoutJson { RefreshToken = login.ResponseToken.RefreshToken }, login.ResponseToken.Token);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(1);
    }

    [Fact]
    public async Task FailedRecoveryCommit_ShouldSendNothing_AndRollbackThrottle()
    {
        var user = await factory.SeedUserAsync();
        await using (var failure = await M8WriteFailure.Install(factory, "PasswordResetTokens", "UserId", user.Id))
        {
            using var failed = await _client.PostAsJsonAsync("/user/forgot-password", new { email = user.Email });
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        factory.SecurityMail.Messages.Any(m => m.Recipient == user.Email).ShouldBeFalse();
        (await factory.ReadDb(db => db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id))).ShouldBe(0);
        (await factory.ReadDb(db => db.RecoveryThrottles.CountAsync(t => t.KeyHash == SecurityRules.Hash(user.Email)))).ShouldBe(0);
        (await factory.RequestRecovery(_client, user)).ShouldNotBeEmpty();
    }
}
