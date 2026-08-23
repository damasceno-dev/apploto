using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using server.Domain.Entities;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

[Collection(ServerApiCollection.Name)]
public sealed class IdempotencyRequestCleanupTest(ServerWebApplicationFactory factory)
{
    private static readonly TimeSpan TestSweepInterval = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Cleanup_ShouldDeleteExpiredRowsInBoundedBatchesAndPreserveUnexpiredRows()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("IdempotencyCleanup");
        var utcNow = DateTime.UtcNow;
        var firstExpired = await SeedRequest(branch.Id, user.Id, utcNow.AddMinutes(-2));
        var secondExpired = await SeedRequest(branch.Id, user.Id, utcNow.AddMinutes(-1));
        var unexpired = await SeedRequest(branch.Id, user.Id, utcNow.AddHours(1));
        await using var cleanupFactory = CreateCleanupFactory(batchSize: 1);

        _ = cleanupFactory.Services;

        await WaitUntilAsync(async () =>
            await factory.ReloadAsync<IdempotencyRequest>(firstExpired.Id) is null &&
            await factory.ReloadAsync<IdempotencyRequest>(secondExpired.Id) is null);
        var persistedUnexpired = await factory.ReloadAsync<IdempotencyRequest>(unexpired.Id);
        persistedUnexpired.ShouldNotBeNull();
        persistedUnexpired.ExpiresAt.ShouldBeGreaterThan(utcNow);
    }

    [Fact]
    public async Task Cleanup_ShouldPreserveCandidate_WhenScopeIsRefreshedBeforeLockIsGranted()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("IdempotencyCleanupRace");
        var request = await SeedRequest(branch.Id, user.Id, DateTime.UtcNow.AddMinutes(-1));
        var refreshedExpiry = DateTime.UtcNow.AddHours(1);
        await using var heldLock = await HoldLockAndRefreshWithoutCommit(request, refreshedExpiry);
        await using var cleanupFactory = CreateCleanupFactory(batchSize: 100);

        _ = cleanupFactory.Services;
        await heldLock.WaitForWaiterAsync();
        await heldLock.CommitAsync();
        await Task.Delay(TestSweepInterval * 3);

        var persisted = await factory.ReloadAsync<IdempotencyRequest>(request.Id);
        persisted.ShouldNotBeNull();
        persisted.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(30));
    }

    private async Task<IdempotencyRequest> SeedRequest(
        Guid branchId,
        Guid userId,
        DateTime expiresAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var request = new IdempotencyRequest
        {
            Endpoint = $"POST /cleanup-test/{Guid.NewGuid():N}",
            Key = $"cleanup-{Guid.NewGuid():N}",
            PayloadHash = new string('A', 64),
            ResourceId = Guid.NewGuid(),
            ResponseEnvelope = "{}",
            ExpiresAt = expiresAt,
            BranchId = branchId,
            UserId = userId
        };
        dbContext.IdempotencyRequests.Add(request);
        await dbContext.SaveChangesAsync();
        return request;
    }

    private async Task<HeldIdempotencyRequestLock> HoldLockAndRefreshWithoutCommit(
        IdempotencyRequest request,
        DateTime refreshedExpiry)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var lockKey = IdempotencyRequestLockKey.Compute(
            request.Endpoint,
            request.BranchId,
            request.UserId,
            request.Key);

        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("key", lockKey);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await using (var updateCommand = new NpgsqlCommand(
            "UPDATE \"IdempotencyRequests\" SET \"ExpiresAt\" = @expiresAt WHERE \"Id\" = @id",
            connection,
            transaction))
        {
            updateCommand.Parameters.AddWithValue("expiresAt", refreshedExpiry);
            updateCommand.Parameters.AddWithValue("id", request.Id);
            (await updateCommand.ExecuteNonQueryAsync()).ShouldBe(1);
        }

        return new HeldIdempotencyRequestLock(connection, transaction, lockKey);
    }

    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateCleanupFactory(
        int batchSize)
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IdempotencyRequestCleanupOptions>();
            services.AddSingleton(new IdempotencyRequestCleanupOptions(TestSweepInterval, batchSize));
        }));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;

            await Task.Delay(25);
        }

        (await condition()).ShouldBeTrue("The idempotency cleanup did not complete within five seconds.");
    }

    private sealed class HeldIdempotencyRequestLock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long lockKey) : IAsyncDisposable
    {
        private bool _committed;

        public async Task WaitForWaiterAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                await using var command = new NpgsqlCommand(
                    """
                    SELECT COUNT(*)
                    FROM pg_locks
                    WHERE locktype = 'advisory'
                      AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                      AND classid = (((@key >> 32) & 4294967295)::oid)
                      AND objid = ((@key & 4294967295)::oid)
                      AND objsubid = 1
                      AND granted = false
                    """,
                    connection,
                    transaction);
                command.Parameters.AddWithValue("key", lockKey);
                if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0)
                    return;

                await Task.Delay(25);
            }

            throw new TimeoutException("The idempotency cleanup did not wait on the held scope lock.");
        }

        public async Task CommitAsync()
        {
            await transaction.CommitAsync();
            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_committed is false)
                await transaction.RollbackAsync();

            await transaction.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
