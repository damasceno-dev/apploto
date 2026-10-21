using System.Net;
using CommonTestUtilities.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

/// <summary>
/// M8 Phase 8 L1 (LT10): the process-local per-client-network request limiter in front of login, and the forwarded-header
/// trust boundary every limiter and login budget reads. Each scenario owns its host, so its limiter state and clock.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class LoginRequestLimiterHttpTest(ServerWebApplicationFactory factory)
{
    private static readonly DateTimeOffset Start = new(2100, 2, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly LoginProtectionOptions ThreePerMinute = LoginProtectionOptionsFactory.Create("3", null, null, null, null);
    private const string PeerA = "203.0.113.10";
    private const string PeerB = "203.0.113.20";

    [Fact]
    public async Task TheBudget_ShouldRefuseBeforeValidationOrBudgets_AndReopenAtTheExactWindowEnd()
    {
        await using var host = Host(out var clock);
        using var client = host.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            using var invalid = await Login(client, string.Empty, PeerA); // a 400 still spends a request
            invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        var email = $"limited-{Guid.NewGuid():N}@example.com";
        using (var refused = await Login(client, email, PeerA))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(refused).ShouldBe("60");
            (await refused.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.SECURITY_RATE_LIMITED);
        }

        // Refused before the use case: no persistent budget row exists for that email.
        var account = LoginAttemptGuard.AccountKey(email);
        (await factory.ReadDb(db => db.LoginThrottles.CountAsync(t => t.AccountKeyHash == account))).ShouldBe(0);
        using (var otherNetwork = await Login(client, string.Empty, PeerB))
            otherNetwork.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        clock.Advance(TimeSpan.FromSeconds(59));
        using (var lastSecond = await Login(client, email, PeerA))
        {
            lastSecond.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(lastSecond).ShouldBe("1");
        }

        clock.Advance(TimeSpan.FromSeconds(1));
        using var reopened = await Login(client, email, PeerA);
        reopened.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("2001:db8:7:7::1", "2001:db8:7:7:abcd::2", "2001:db8:7:8::1")]
    [InlineData("198.51.100.7", "::ffff:198.51.100.7", "198.51.100.8")]
    public async Task Partitions_ShouldBeTheIPv4AddressOrTheIPv6Slash64(string first, string sameNetwork, string otherNetwork)
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        await Spend(client, first);

        using (var shared = await Login(client, string.Empty, sameNetwork))
            shared.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        using var separate = await Login(client, string.Empty, otherNetwork);
        separate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ForwardedFor_ShouldBeIgnored_WithoutAConfiguredProxy()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        await Spend(client, PeerA);

        using var claimed = await Login(client, string.Empty, PeerB, forwardedFor: "198.51.100.200");
        claimed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var spoofed = await Login(client, string.Empty, PeerB, forwardedFor: PeerA); // counted as PeerB, not PeerA
        spoofed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "10.0.0.5", "10.0.0.5")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/8", "10.20.30.40")]
    public async Task ForwardedFor_ShouldBeHonored_OnlyFromATrustedProxy(string key, string value, string proxy)
    {
        await using var host = Host(out _, new Dictionary<string, string?> { [key] = value });
        using var client = host.CreateClient();
        await Spend(client, PeerA);

        using (var throughProxy = await Login(client, string.Empty, proxy, forwardedFor: PeerA))
            throughProxy.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        using (var untrusted = await Login(client, string.Empty, PeerB, forwardedFor: PeerA))
            untrusted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // The first untrusted hop is the client: a chain cannot launder PeerA behind a trusted proxy.
        using var chained = await Login(client, string.Empty, proxy, forwardedFor: $"{PeerA}, 198.51.100.77");
        chained.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task APasswordReset_ShouldClearThePersistentBudgets_ButNotTheNetworksRequestLimit()
    {
        await using var host = Host(out var clock);
        using var client = host.CreateClient();
        var user = await factory.SeedUserAsync();
        await Spend(client, PeerA);
        using (var forgot = await client.PostFromPeerAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = user.Email }, PeerA))
            forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var capability = await host.Services.GetRequiredService<SecurityMailCapture>().WaitToken(user.Email);
        using (var reset = await client.PostFromPeerAsync("/user/reset-password",
                   new RequestResetPasswordJson { Token = capability, NewPassword = "ResetPassword123" }, PeerA))
            reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var stillLimited = await client.PostFromPeerAsync("/user/login",
                   new RequestUserLoginJson { Email = user.Email, Password = "ResetPassword123" }, PeerA))
        {
            stillLimited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            int.Parse(RetryAfter(stillLimited)!).ShouldBeInRange(1, 60);
        }

        clock.Advance(TimeSpan.FromMinutes(1));
        using var admitted = await client.PostFromPeerAsync("/user/login",
            new RequestUserLoginJson { Email = user.Email, Password = "ResetPassword123" }, PeerA);
        admitted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CapabilityRefusals_ShouldCarryRetryAfter_WhileRecoveryStaysAnEmpty202()
    {
        await using var host = Host(out _);
        using var client = host.CreateClient();
        for (var i = 0; i < 120; i++)
        {
            using var invalid = await client.PostFromPeerAsync("/invitation/preview", new RequestInvitationTokenJson { Token = string.Empty }, PeerA);
            invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using (var refused = await client.PostFromPeerAsync("/invitation/preview", new RequestInvitationTokenJson { Token = string.Empty }, PeerA))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
            RetryAfter(refused).ShouldBe("60");
        }

        for (var i = 0; i < 60; i++)
        {
            using var accepted = await client.PostFromPeerAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = string.Empty }, PeerB);
            accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        using var suppressed = await client.PostFromPeerAsync("/user/forgot-password",
            new RequestForgotPasswordJson { Email = $"r-{Guid.NewGuid():N}@example.com" }, PeerB);
        suppressed.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        RetryAfter(suppressed).ShouldBeNull();
        (await suppressed.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    private WebApplicationFactory<Program> Host(out SettableUtcTimeProvider clock, Dictionary<string, string?>? configuration = null)
    {
        var time = clock = new SettableUtcTimeProvider(Start);
        return factory.WithWebHostBuilder(builder =>
        {
            if (configuration is not null)
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(time);
                services.RemoveAll<LoginProtectionOptions>();
                services.AddSingleton(ThreePerMinute);
            });
        });
    }

    private static async Task Spend(HttpClient client, string peer)
    {
        for (var i = 0; i < 3; i++)
        {
            using var response = await Login(client, string.Empty, peer);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string peer, string? forwardedFor = null) =>
        client.PostFromPeerAsync("/user/login", new RequestUserLoginJson { Email = email, Password = "WrongPassword123" }, peer, forwardedFor);

    private static string? RetryAfter(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Retry-After", out var values) ? values.Single() : null;
}
