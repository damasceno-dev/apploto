using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

/// <summary>
/// M8 Phase 8 persistent login budgets (LT1–LT9, LT11) over real HTTP and PostgreSQL. Each scenario owns a host whose
/// clock it moves explicitly. The clock starts far from wall-clock time, so the real-time retention sweep of any other
/// host can never expire these rows mid-test.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class LoginThrottleHttpTest(ServerWebApplicationFactory factory)
{
    private static readonly DateTimeOffset Start = new(2100, 1, 4, 9, 0, 0, TimeSpan.Zero);
    // Three failures per network, six per account, a ten-minute window and a twenty-minute block.
    private static readonly LoginProtectionOptions Budgets = LoginProtectionOptionsFactory.Create(
        LoginProtectionOptionsFactory.MaxRequestsPerMinute.ToString(), "3", "6", "10", "20");
    private const string Cooldown = "1200";
    private const string PeerA = "198.51.100.10";
    private const string PeerB = "198.51.100.20";
    private const string PeerC = "198.51.100.30";
    private const string WrongPassword = "WrongPassword123";

    [Fact]
    public async Task EveryAccountState_ShouldSeeIdenticalFailures_ThenIdenticalRefusals()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var active = await factory.SeedUserAsync();
        var inactive = await factory.SeedUserAsync();
        var passwordless = await factory.SeedUserAsync();
        await factory.UpdateDb(async db =>
        {
            (await db.Users.FindAsync(inactive.Id))!.Active = false;
            (await db.Users.FindAsync(passwordless.Id))!.Password = string.Empty;
        });
        var emails = new[] { $"missing-{Guid.NewGuid():N}@example.com", inactive.Email, passwordless.Email, active.Email };

        var failures = new List<string>();
        var refusals = new List<(string Body, string? RetryAfter)>();
        foreach (var email in emails)
        {
            for (var i = 0; i < 3; i++)
            {
                using var failed = await Login(client, email, WrongPassword, PeerA);
                failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
                failures.Add(await failed.Content.ReadAsStringAsync());
            }

            // Even the right password of the active account is refused once its budget is spent.
            using var refused = await Login(client, email, email == active.Email ? TestSeeder.DefaultPassword : WrongPassword, PeerA);
            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            refusals.Add((await refused.Content.ReadAsStringAsync(), RetryAfter(refused)));
        }

        failures.Distinct().ShouldHaveSingleItem().ShouldContain(ResourcesErrorMessages.PASSWORD_WRONG);
        var refusal = refusals.Distinct().ShouldHaveSingleItem();
        refusal.Body.ShouldContain(ResourcesErrorMessages.SECURITY_RATE_LIMITED);
        refusal.RetryAfter.ShouldBe(Cooldown);
        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == active.Id))).ShouldBe(0);
        foreach (var email in emails)
        {
            var key = LoginAttemptGuard.AccountKey(email);
            var rows = await factory.ReadDb(db => db.LoginThrottles.Where(t => t.AccountKeyHash == key).ToListAsync());
            rows.Count.ShouldBe(2);
            rows.ShouldAllBe(r => r.FailureCount == 3 && r.KeyHash.Length == 64);
        }
    }

    [Fact]
    public async Task ABlockedAccount_ShouldRefuseTheRightPassword_UntilTheExactEnd_MintingNothing()
    {
        await using var host = Host(out var clock);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        for (var i = 0; i < 3; i++)
        {
            using var failed = await Login(client, user.Email, WrongPassword, PeerA);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using (var refused = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(refused).ShouldBe(Cooldown);
        }

        clock.Advance(TimeSpan.FromSeconds(1199));
        using (var last = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA))
        {
            last.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(last).ShouldBe("1");
        }

        (await factory.ReadDb(db => db.RefreshTokens.CountAsync(t => t.UserId == user.Id))).ShouldBe(0);
        clock.Advance(TimeSpan.FromSeconds(1));
        using var admitted = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA);
        admitted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await admitted.ReadContentAsync<ResponseUserLoginJson>()).UserId.ShouldBe(user.Id);

        // Success removed this network's row only; the account's own count runs out by itself.
        var account = LoginAttemptGuard.AccountKey(user.Email);
        var remaining = await factory.ReadDb(db => db.LoginThrottles.Where(t => t.AccountKeyHash == account).ToListAsync());
        remaining.ShouldHaveSingleItem().KeyHash.ShouldBe(account);
        remaining[0].FailureCount.ShouldBe(3);
    }

    [Fact]
    public async Task EmailVariants_ShouldSpendOneBudget()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var local = $"variant-{Guid.NewGuid():N}";
        var user = await factory.SeedUserAsync($"{local}@example.com");
        foreach (var variant in new[] { $"{local.ToUpperInvariant()}@EXAMPLE.COM", $" {local}@Example.com", $"{local}@example.com " })
        {
            using var failed = await Login(client, variant, WrongPassword, PeerA);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        using var refused = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task ABlockedNetwork_ShouldNotLockTheOwnerOut_UntilTheAccountBudgetIsSpent()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        await FailTimes(client, user.Email, PeerA, 3);
        using (var attacker = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA))
            attacker.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using (var owner = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerB))
            owner.StatusCode.ShouldBe(HttpStatusCode.OK);

        await FailTimes(client, user.Email, PeerC, 3); // six failures across networks spend the account budget
        using var everyone = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerB);
        everyone.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        RetryAfter(everyone).ShouldBe(Cooldown);
    }

    [Fact]
    public async Task IPv6Networks_ShouldBePartitionedBy64()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        await FailTimes(client, user.Email, "2001:db8:1:2::10", 3);

        using (var sameNetwork = await Login(client, user.Email, TestSeeder.DefaultPassword, "2001:db8:1:2:ffff::99"))
            sameNetwork.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        using var otherNetwork = await Login(client, user.Email, TestSeeder.DefaultPassword, "2001:db8:1:3::10");
        otherNetwork.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordResetAndChange_ShouldStartTheAccountsBudgetsOver()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        await FailTimes(client, user.Email, PeerA, 3);
        await FailTimes(client, user.Email, PeerC, 3);
        using (var locked = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerB))
            locked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using (var forgot = await client.PostAsJsonAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = user.Email }))
            forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var capability = await host.Services.GetRequiredService<SecurityMailCapture>().WaitToken(user.Email);
        using (var reset = await client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson { Token = capability, NewPassword = "ResetPassword123" }))
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var account = LoginAttemptGuard.AccountKey(user.Email);
        (await factory.ReadDb(db => db.LoginThrottles.CountAsync(t => t.AccountKeyHash == account))).ShouldBe(0);

        using (var recovered = await Login(client, user.Email, "ResetPassword123", PeerA))
            recovered.StatusCode.ShouldBe(HttpStatusCode.OK);

        await FailTimes(client, user.Email, PeerA, 2);
        using var login = await Login(client, user.Email, "ResetPassword123", PeerB);
        var identity = (await login.ReadContentAsync<ResponseUserLoginJson>()).ResponseToken.Token;
        using (var change = await client.PostAuthAsync("/user/change-password", new RequestChangePasswordJson
               { CurrentPassword = "ResetPassword123", NewPassword = "ChangedPassword123" }, identity))
            change.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await factory.ReadDb(db => db.LoginThrottles.CountAsync(t => t.AccountKeyHash == account))).ShouldBe(0);
    }

    [Fact]
    public async Task AConcurrentBurst_ShouldVerifyExactlyTheBudget()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Login(client, user.Email, WrongPassword, PeerA)));
        var statuses = responses.Select(r => r.StatusCode).ToList();
        foreach (var response in responses) response.Dispose();

        statuses.Count(s => s == HttpStatusCode.Unauthorized).ShouldBe(3);
        statuses.Count(s => s == HttpStatusCode.TooManyRequests).ShouldBe(7);
        var account = LoginAttemptGuard.AccountKey(user.Email);
        (await factory.ReadDb(db => db.LoginThrottles.Where(t => t.AccountKeyHash == account).Select(t => t.FailureCount).ToListAsync()))
            .ShouldAllBe(count => count == 3);
    }

    [Fact]
    public async Task GoogleFirstLinkProof_ShouldSpendTheSameBudget_AndConsumeNoChallenge()
    {
        var verifier = new TestGoogleVerifier();
        await using var host = Host(out var clock, verifier);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        var challenge = await Challenge(client);
        var token = verifier.Accept(user.Email, "subject-" + Guid.NewGuid().ToString("N"), challenge.Nonce);

        using (var noProof = await client.PostFromPeerAsync("/user/google/sign-in", SignIn(challenge, token, null), PeerA))
            noProof.StatusCode.ShouldBe(HttpStatusCode.Conflict); // asking for the password spends nothing
        for (var i = 0; i < 3; i++)
        {
            using var wrong = await client.PostFromPeerAsync("/user/google/sign-in", SignIn(challenge, token, WrongPassword), PeerA);
            wrong.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await wrong.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.GOOGLE_LINK_PROOF_REQUIRED);
        }

        using (var refused = await client.PostFromPeerAsync("/user/google/sign-in", SignIn(challenge, token, TestSeeder.DefaultPassword), PeerA))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(refused).ShouldBe(Cooldown);
        }

        using (var password = await Login(client, user.Email, TestSeeder.DefaultPassword, PeerA))
            password.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await factory.ReadDb(db => db.GoogleLoginChallenges.SingleAsync(c => c.Id == challenge.ChallengeId))).ConsumedAt.ShouldBeNull();
        (await factory.ReadDb(db => db.GoogleIdentities.CountAsync(i => i.UserId == user.Id))).ShouldBe(0);

        clock.Advance(TimeSpan.FromMinutes(20));
        var fresh = await Challenge(client);
        var freshToken = verifier.Accept(user.Email, verifier.Subject(token), fresh.Nonce);
        using var linked = await client.PostFromPeerAsync("/user/google/sign-in", SignIn(fresh, freshToken, TestSeeder.DefaultPassword), PeerA);
        linked.StatusCode.ShouldBe(HttpStatusCode.OK, await linked.Content.ReadAsStringAsync());
        (await factory.ReadDb(db => db.GoogleIdentities.CountAsync(i => i.UserId == user.Id))).ShouldBe(1);
    }

    private WebApplicationFactory<Program> Host(out SettableUtcTimeProvider clock, IGoogleIdentityVerifier? verifier = null)
    {
        var time = clock = new SettableUtcTimeProvider(Start);
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(time);
            services.RemoveAll<LoginProtectionOptions>();
            services.AddSingleton(Budgets);
            if (verifier is null) return;
            services.RemoveAll<IGoogleIdentityVerifier>();
            services.AddSingleton(verifier);
        }));
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password, string peer) =>
        client.PostFromPeerAsync("/user/login", new RequestUserLoginJson { Email = email, Password = password }, peer);

    private static async Task FailTimes(HttpClient client, string email, string peer, int times)
    {
        for (var i = 0; i < times; i++)
        {
            using var failed = await Login(client, email, WrongPassword, peer);
            failed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private static string? RetryAfter(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Retry-After", out var values) ? values.Single() : null;

    private static async Task<ResponseGoogleChallengeJson> Challenge(HttpClient client)
    {
        using var response = await client.PostFromPeerAsync<object?>("/user/google/challenge", null, PeerA);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadContentAsync<ResponseGoogleChallengeJson>();
    }

    private static RequestGoogleSignInJson SignIn(ResponseGoogleChallengeJson challenge, string idToken, string? password) => new()
    {
        ChallengeId = challenge.ChallengeId, State = challenge.State, IdToken = idToken, CurrentPassword = password
    };
}
