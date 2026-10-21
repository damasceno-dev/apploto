using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M8 Phase 8 retention (LT12) on real PostgreSQL: the sweep deletes login rows at or past <c>ExpiresAt</c> and
/// recovery rows whose cooldown has elapsed, keeps live rows, and skips any row a request holds <c>FOR UPDATE</c>.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class SecurityThrottleCleanupTest(ServerWebApplicationFactory factory)
{
    // Later than every instant other tests write, so earlier rows drain first and never hide these in a batch.
    private static readonly DateTime Now = new(2200, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Sweep_ShouldDeleteOnlyExpiredRows_InBoundedBatches()
    {
        var expired = Login(Now.AddSeconds(-1));
        var boundary = Login(Now);
        var live = Login(Now.AddSeconds(1));
        var elapsed = new RecoveryThrottle { KeyHash = Hash(), NextAllowedAt = Now };
        var cooling = new RecoveryThrottle { KeyHash = Hash(), NextAllowedAt = Now.AddSeconds(1) };
        await factory.UpdateDb(async db =>
        {
            await db.LoginThrottles.AddRangeAsync(expired, boundary, live);
            await db.RecoveryThrottles.AddRangeAsync(elapsed, cooling);
        });

        await Drain(Now);

        var logins = await factory.ReadDb(db => db.LoginThrottles.Where(t => new[] { expired.Id, boundary.Id, live.Id }.Contains(t.Id)).Select(t => t.Id).ToListAsync());
        logins.ShouldBe([live.Id]);
        var recoveries = await factory.ReadDb(db => db.RecoveryThrottles.Where(t => t.Id == elapsed.Id || t.Id == cooling.Id).Select(t => t.Id).ToListAsync());
        recoveries.ShouldBe([cooling.Id]);
    }

    [Fact]
    public async Task Sweep_ShouldSkipARowARequestHolds_AndDeleteItAfterRelease()
    {
        var held = Login(Now.AddDays(-1));
        await factory.UpdateDb(async db => await db.LoginThrottles.AddAsync(held));
        await using (var connection = new NpgsqlConnection(factory.DisposableConnectionString))
        {
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM \"LoginThrottles\" WHERE \"Id\" = @id FOR UPDATE", connection, transaction))
            {
                lockRow.Parameters.AddWithValue("id", held.Id);
                await lockRow.ExecuteScalarAsync();
            }

            await Drain(Now);
            (await factory.ReadDb(db => db.LoginThrottles.CountAsync(t => t.Id == held.Id))).ShouldBe(1);
            await transaction.RollbackAsync();
        }

        await Drain(Now);
        (await factory.ReadDb(db => db.LoginThrottles.CountAsync(t => t.Id == held.Id))).ShouldBe(0);
    }

    [Fact]
    public void Defaults_ShouldSweepEveryFifteenMinutes_InBatchesOf500()
    {
        var options = factory.Services.GetRequiredService<SecurityThrottleCleanupOptions>();
        options.SweepInterval.ShouldBe(TimeSpan.FromMinutes(15));
        options.BatchSize.ShouldBe(500);
    }

    private async Task Drain(DateTime now)
    {
        using var scope = factory.Services.CreateScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<SecurityThrottleCleanup>();
        SecurityThrottleCleanupResult result;
        do result = await cleanup.DeleteExpiredBatch(now, 2);
        while (result.LoginDeleted == 2 || result.RecoveryDeleted == 2);
    }

    private static LoginThrottle Login(DateTime expiresAt) => new()
    {
        KeyHash = Hash(), AccountKeyHash = Hash(), FailureCount = 1, WindowStartedAt = expiresAt.AddMinutes(-15), ExpiresAt = expiresAt
    };

    private static string Hash() => Convert.ToHexString(Guid.NewGuid().ToByteArray()) + Convert.ToHexString(Guid.NewGuid().ToByteArray());
}
