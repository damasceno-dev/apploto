using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Application.Services.Security;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.AccountSecurity;

[Collection(ServerApiCollection.Name)]
public sealed class RecoveryRateLimitHttpTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task PerIpThrottle_ShouldKeep202Semantics_AndStopWritesForNewTargets()
    {
        // Separate host owns its limiter; saturating it never affects another test's host or user process.
        await using var host = factory.WithWebHostBuilder(_ => { });
        using var client = host.CreateClient();
        var target = $"rate-{Guid.NewGuid():N}@example.invalid";
        var responses = await Task.WhenAll(Enumerable.Range(0, 60).Select(_ => client.PostAsJsonAsync("/user/forgot-password", new { email = target })));
        foreach (var response in responses) { response.StatusCode.ShouldBe(HttpStatusCode.Accepted); response.Dispose(); }
        var blockedTarget = $"rate-blocked-{Guid.NewGuid():N}@example.invalid";
        using var blocked = await client.PostAsJsonAsync("/user/forgot-password", new { email = blockedTarget });
        blocked.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await blocked.Content.ReadAsStringAsync()).ShouldBeEmpty();
        (await factory.ReadDb(db => db.RecoveryThrottles.CountAsync(t => t.KeyHash == SecurityRules.Hash(blockedTarget)))).ShouldBe(0);
    }
}
