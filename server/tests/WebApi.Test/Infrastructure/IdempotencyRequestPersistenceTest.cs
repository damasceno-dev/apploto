using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Interfaces;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

[Collection(ServerApiCollection.Name)]
public sealed class IdempotencyRequestPersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task MigratedDatabase_ShouldContainIdempotencyReplayStoreAndScopeUniqueness()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('\"IdempotencyRequests\"') IS NOT NULL";
        Convert.ToBoolean(await command.ExecuteScalarAsync()).ShouldBeTrue();

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE tablename = 'IdempotencyRequests'
              AND indexname = 'IX_IdempotencyRequests_Endpoint_BranchId_UserId_Key'
              AND indexdef LIKE 'CREATE UNIQUE INDEX%'
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(1);

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conrelid = '"IdempotencyRequests"'::regclass
              AND contype = 'f'
              AND confdeltype = 'r'
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task ScopeLock_ShouldReportUnavailable_WhenPostgresLockWaitTimesOut()
    {
        var endpoint = "POST /transaction";
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = $"held-{Guid.NewGuid():N}";

        await using var holderScope = factory.Services.CreateAsyncScope();
        var holderContext = holderScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = holderContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        await using var holderConnection = new NpgsqlConnection(connectionString);
        await holderConnection.OpenAsync();
        await using var holderTransaction = await holderConnection.BeginTransactionAsync();
        var lockKey = IdempotencyRequestLockKey.Compute(endpoint, branchId, userId, key);
        await using (var lockCommand = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(@key)",
            holderConnection,
            holderTransaction))
        {
            lockCommand.Parameters.AddWithValue("key", lockKey);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await using var contenderScope = factory.Services.CreateAsyncScope();
        var contenderContext = contenderScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var requestsRepository = contenderScope.ServiceProvider
            .GetRequiredService<IIdempotencyRequestsRepository>();
        await using var contenderTransaction = await contenderContext.Database.BeginTransactionAsync();
        await contenderContext.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '100ms'");

        (await requestsRepository.TryAcquireScopeLock(endpoint, branchId, userId, key)).ShouldBeFalse();
    }
}
