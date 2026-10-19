using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using server.Application.Services;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

[Collection(ServerApiCollection.Name)]
public sealed class AccountSecurityHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_ShouldNotDiscloseMissing_Inactive_OrPasswordlessAccounts()
    {
        var active = await factory.SeedUserAsync();
        var inactive = await factory.SeedUserAsync();
        var passwordless = await factory.SeedUserAsync();
        await factory.UpdateDb(async db =>
        {
            (await db.Users.FindAsync(inactive.Id))!.Active = false;
            (await db.Users.FindAsync(passwordless.Id))!.Password = string.Empty;
        });
        var responses = new List<string>();
        foreach (var email in new[] { $"missing-{Guid.NewGuid():N}@example.invalid", inactive.Email,
                     passwordless.Email, active.Email })
        {
            using var response = await _client.PostAsJsonAsync("/user/login", new RequestUserLoginJson
            { Email = email, Password = "WrongPassword123" });
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            responses.Add(await response.Content.ReadAsStringAsync());
        }
        responses.Distinct().Count().ShouldBe(1);
        responses[0].ShouldContain(ResourcesErrorMessages.PASSWORD_WRONG);
    }

    [Fact]
    public async Task Profile_ShouldUseIdentity_ExposeOnlySafeFields_AndChangeOnlyName()
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        var identity = login.ResponseToken.Token;
        using var read = await _client.GetAuthAsync("/user/profile", identity);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        document.RootElement.EnumerateObject().Select(p => p.Name).Order().ShouldBe(["email", "hasPassword", "isPlatformOperator", "name", "userId"]);
        using var update = await _client.PutAuthAsync("/user/profile", new { name = "  Nome atualizado  ", email = "forged@example.invalid", isPlatformOperator = true }, identity);
        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await update.ReadContentAsync<ResponseUserProfileJson>();
        profile.UserId.ShouldBe(user.Id);
        profile.Name.ShouldBe("Nome atualizado");
        profile.Email.ShouldBe(user.Email);
        profile.IsPlatformOperator.ShouldBeFalse();
        profile.HasPassword.ShouldBeTrue();
        (await factory.ReloadAsync<User>(user.Id))!.Name.ShouldBe(profile.Name);
        using var invalid = await _client.PutAuthAsync("/user/profile", new { name = " " }, identity);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_ShouldRequireCurrentPassword_RevokeEverySession_AndKeepIdentityUntilExpiry()
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        var second = await factory.SeedRefreshTokenAsync(user.Id); // Explicitly prove more than one active row is revoked.
        using var wrong = await _client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
        { CurrentPassword = "incorrect", NewPassword = "NewPassword123" }, login.ResponseToken.Token);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.ReloadAsync<RefreshToken>(second.Id))!.Active.ShouldBeTrue();
        using var change = await _client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
        { CurrentPassword = TestSeeder.DefaultPassword, NewPassword = "NewPassword123" }, login.ResponseToken.Token);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(0);
        using var renewal = await Renew(login.ResponseToken.RefreshToken);
        renewal.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var profile = await _client.GetAuthAsync("/user/profile", login.ResponseToken.Token);
        profile.StatusCode.ShouldBe(HttpStatusCode.OK);
        await factory.Login(_client, user, "NewPassword123");
        using var oldPassword = await _client.PostAsJsonAsync("/user/login", new RequestUserLoginJson { Email = user.Email, Password = TestSeeder.DefaultPassword });
        oldPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ShouldRevokeOnlyContractedSession_AndIgnoreForeignOrUnknownToken()
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        var second = await factory.SeedRefreshTokenAsync(user.Id);
        var other = await factory.SeedUserAsync();
        var foreign = await factory.SeedRefreshTokenAsync(other.Id);
        foreach (var value in new[] { foreign.Value, "unknown-refresh", login.ResponseToken.RefreshToken, login.ResponseToken.RefreshToken })
        {
            using var logout = await _client.PostAuthAsync("/user/logout", new RequestLogoutJson { RefreshToken = value }, login.ResponseToken.Token);
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }
        (await factory.ReloadAsync<RefreshToken>(second.Id))!.Active.ShouldBeTrue();
        (await factory.ReloadAsync<RefreshToken>(foreign.Id))!.Active.ShouldBeTrue();
        using var renewal = await Renew(login.ResponseToken.RefreshToken);
        renewal.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecoveryRequest_ShouldHaveEqualSemantics_ThrottleEveryTarget_AndStoreOnlyHashes()
    {
        var user = await factory.SeedUserAsync();
        var inactive = await factory.SeedUserAsync();
        await factory.UpdateDb(async db => (await db.Users.FindAsync(inactive.Id))!.Active = false);
        var absent = $"missing-{Guid.NewGuid():N}@example.invalid";
        foreach (var email in new[] { user.Email, inactive.Email, absent, user.Email.ToUpperInvariant(), absent })
        {
            using var response = await _client.PostAsJsonAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = email });
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
            response.Headers.Contains("Retry-After").ShouldBeFalse();
        }
        var token = await factory.SecurityMail.WaitToken(user.Email);
        token.Length.ShouldBe(43);
        var saved = await factory.ReadDb(db => db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id));
        saved.TokenHash.ShouldBe(SecurityRules.Hash(token));
        saved.TokenHash.ShouldNotBe(token);
        saved.ExpiresAt.Kind.ShouldBe(DateTimeKind.Utc);
        saved.ExpiresAt.ShouldBe(saved.CreatedAt.AddMinutes(30));
        (await factory.ReadDb(db => db.PasswordResetTokens.CountAsync(t => t.UserId == inactive.Id))).ShouldBe(0);
        factory.SecurityMail.Messages.Count(m => m.Recipient == user.Email).ShouldBe(1);
        factory.SecurityMail.Messages.Any(m => m.Recipient == inactive.Email || m.Recipient == absent).ShouldBeFalse();
        foreach (var email in new[] { user.Email, inactive.Email, absent })
            (await factory.ReadDb(db => db.RecoveryThrottles.CountAsync(t => t.KeyHash == SecurityRules.Hash(email)))).ShouldBe(1);
    }

    [Fact]
    public async Task Recovery_ShouldIgnoreHostHeaders_AndNeverClaimDeliveryWhenDisabledOrFailed()
    {
        var user = await factory.SeedUserAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/user/forgot-password")
        { Content = JsonContent.Create(new RequestForgotPasswordJson { Email = user.Email }) };
        request.Headers.Host = "attacker.invalid";
        using var response = await _client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var token = await factory.SecurityMail.WaitToken(user.Email);
        factory.SecurityMail.Messages.Single(m => m.Recipient == user.Email).Body.ShouldContain("http://127.0.0.1:3000/reset-password#token=");
        factory.SecurityMail.Messages.Single(m => m.Recipient == user.Email).Body.ShouldNotContain("attacker.invalid");
        var failed = await factory.SeedUserAsync();
        factory.SecurityMail.FailRecipients.TryAdd(failed.Email, true);
        using var failure = await _client.PostAsJsonAsync("/user/forgot-password", new { email = failed.Email });
        failure.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await failure.Content.ReadAsStringAsync()).ShouldBeEmpty();
        (await factory.ReadDb(db => db.PasswordResetTokens.CountAsync(t => t.UserId == failed.Id))).ShouldBe(1);
        token.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("inactive")]
    [InlineData("missing")]
    [InlineData("invalid")]
    public async Task Reset_ShouldRefuseInvalidExpiredOrInactiveCapabilities(string scenario)
    {
        var user = await factory.SeedUserAsync();
        var token = await factory.RequestRecovery(_client, user);
        await factory.UpdateDb(async db =>
        {
            if (scenario == "expired")
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"PasswordResetTokens\" SET \"ExpiresAt\" = now() - interval '1 second' WHERE \"UserId\" = {user.Id}");
            if (scenario == "inactive") (await db.Users.FindAsync(user.Id))!.Active = false;
            if (scenario == "missing") db.PasswordResetTokens.RemoveRange(await db.PasswordResetTokens.Where(t => t.UserId == user.Id).ToListAsync());
        });
        using var reset = await Reset(scenario == "invalid" ? SecurityRules.NewToken() : token);
        reset.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await reset.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldBe([ResourcesErrorMessages.SECURITY_TOKEN_INVALID]);
        new PasswordEncryption().VerifyPassword(TestSeeder.DefaultPassword, (await factory.ReloadAsync<User>(user.Id))!.Password).ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_ShouldBeSingleUse_RevokeAllSessions_AndNotLoginAutomatically()
    {
        var user = await factory.SeedUserAsync();
        var first = await factory.SeedRefreshTokenAsync(user.Id);
        var second = await factory.SeedRefreshTokenAsync(user.Id);
        var token = await factory.RequestRecovery(_client, user);
        using var reset = await Reset(token);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await reset.Content.ReadAsStringAsync()).ShouldBeEmpty();
        (await factory.ReloadAsync<RefreshToken>(first.Id))!.Active.ShouldBeFalse();
        (await factory.ReloadAsync<RefreshToken>(second.Id))!.Active.ShouldBeFalse();
        var stored = await factory.ReadDb(db => db.PasswordResetTokens.SingleAsync(t => t.UserId == user.Id));
        stored.Active.ShouldBeFalse();
        stored.ConsumedAt.ShouldNotBeNull();
        using var replay = await Reset(token);
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await factory.Login(_client, user, "NewPassword123");
    }

    [Fact]
    public async Task NewRecoveryAfterCooldown_ShouldInvalidateOldCapability()
    {
        var user = await factory.SeedUserAsync();
        var old = await factory.RequestRecovery(_client, user);
        await factory.UpdateDb(async db => (await db.RecoveryThrottles.SingleAsync(t => t.KeyHash == SecurityRules.Hash(user.Email))).NextAllowedAt = DateTime.UtcNow.AddSeconds(-1));
        var current = await factory.RequestRecovery(_client, user, index: 1);
        current.ShouldNotBe(old);
        using var replay = await Reset(old);
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var reset = await Reset(current);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SimultaneousReset_ShouldConsumeExactlyOnce_InPostgres()
    {
        var user = await factory.SeedUserAsync();
        var token = await factory.RequestRecovery(_client, user);
        await using var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email));
        var first = Reset(token);
        await held.WaitForWaitersAsync(1);
        var second = Reset(token);
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        r2.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReadDb(db => db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id && t.ConsumedAt != null))).ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ResetRenewalRace_ShouldLeaveNoActiveRefreshSession(bool resetFirst)
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        var token = await factory.RequestRecovery(_client, user);
        await using var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email));
        Task<HttpResponseMessage> first = resetFirst ? Reset(token) : Renew(login.ResponseToken.RefreshToken);
        await held.WaitForWaitersAsync(1);
        Task<HttpResponseMessage> second = resetFirst ? Renew(login.ResponseToken.RefreshToken) : Reset(token);
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(resetFirst ? HttpStatusCode.NoContent : HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(resetFirst ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LogoutRenewalRace_ShouldRevokeTheStableSessionInBothOrders(bool logoutFirst)
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        await using var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email));
        Task<HttpResponseMessage> Logout() => _client.PostAuthAsync("/user/logout", new RequestLogoutJson { RefreshToken = login.ResponseToken.RefreshToken }, login.ResponseToken.Token);
        var first = logoutFirst ? Logout() : Renew(login.ResponseToken.RefreshToken);
        await held.WaitForWaitersAsync(1);
        var second = logoutFirst ? Renew(login.ResponseToken.RefreshToken) : Logout();
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(logoutFirst ? HttpStatusCode.NoContent : HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(logoutFirst ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangePasswordRenewalRace_ShouldRevokeEverySession(bool passwordFirst)
    {
        var user = await factory.SeedUserAsync();
        var login = await factory.Login(_client, user);
        await using var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email));
        Task<HttpResponseMessage> Change() => _client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
        { CurrentPassword = TestSeeder.DefaultPassword, NewPassword = "NewPassword123" }, login.ResponseToken.Token);
        var first = passwordFirst ? Change() : Renew(login.ResponseToken.RefreshToken);
        await held.WaitForWaitersAsync(1);
        var second = passwordFirst ? Renew(login.ResponseToken.RefreshToken) : Change();
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        using var r1 = await first;
        using var r2 = await second;
        r1.StatusCode.ShouldBe(passwordFirst ? HttpStatusCode.NoContent : HttpStatusCode.OK);
        r2.StatusCode.ShouldBe(passwordFirst ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id && t.Active))).ShouldBe(0);
    }

    [Fact]
    public async Task BusyAccountCoordination_ShouldReturnDedicated409_WithoutChangingProfile()
    {
        var user = await factory.SeedUserAsync();
        await using var held = await factory.HoldSecurity(SecurityRules.EmailKey(user.Email));
        using var busy = await _client.PutAuthAsync("/user/profile", new RequestUpdateProfileJson { Name = "Should not persist" }, factory.IssueGlobalToken(user));
        busy.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await busy.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SECURITY_COORDINATION_BUSY]);
        (await factory.ReloadAsync<User>(user.Id))!.Name.ShouldBe(user.Name);
    }

    private Task<HttpResponseMessage> Renew(string token) => _client.PostAsJsonAsync("/user/renew-token", new RequestUserRenewTokenJson { Value = token });
    private Task<HttpResponseMessage> Reset(string token) => _client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson { Token = token, NewPassword = "NewPassword123" });
}
