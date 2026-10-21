using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M8 Phase 8 upgrade on a real PostgreSQL database: an additive, empty <c>LoginThrottles</c> table with hash-only
/// columns, the sweep indexes, untouched existing recovery rows, a positive-count guard and a clean downgrade.
/// </summary>
public sealed class M8Phase8LoginThrottleMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261020234602_M11Phase6MonthlyFinancialRole";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m8_phase8").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldAddAnEmptyHashOnlyTable_KeepRecoveryRows_AndDowngradeCleanly()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var recoveryId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"RecoveryThrottles\" (\"Id\", \"KeyHash\", \"NextAllowedAt\", \"CreatedAt\", \"Active\") VALUES ({recoveryId}, {new string('A', 64)}, now(), now(), TRUE)");

        await db.Database.MigrateAsync();

        (await db.LoginThrottles.CountAsync()).ShouldBe(0);
        (await db.RecoveryThrottles.CountAsync(t => t.Id == recoveryId)).ShouldBe(1);
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await Strings("SELECT column_name FROM information_schema.columns WHERE table_name = 'LoginThrottles'"))
            .Order().ShouldBe(["AccountKeyHash", "Active", "BlockedUntil", "CreatedAt", "ExpiresAt", "FailureCount", "Id", "KeyHash", "WindowStartedAt"]);
        (await Strings("SELECT indexname FROM pg_indexes WHERE tablename IN ('LoginThrottles', 'RecoveryThrottles')"))
            .Order(StringComparer.Ordinal).ShouldBe(["IX_LoginThrottles_AccountKeyHash", "IX_LoginThrottles_ExpiresAt",
                "IX_LoginThrottles_KeyHash", "IX_RecoveryThrottles_KeyHash", "IX_RecoveryThrottles_NextAllowedAt",
                "PK_LoginThrottles", "PK_RecoveryThrottles"]);
        var error = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO "LoginThrottles" ("Id", "KeyHash", "AccountKeyHash", "FailureCount", "WindowStartedAt", "ExpiresAt", "CreatedAt", "Active")
            VALUES (gen_random_uuid(), 'k', 'a', 0, now(), now(), now(), TRUE)
            """));
        error.ConstraintName.ShouldBe("CK_LoginThrottles_FailureCount_Positive");

        await migrator.MigrateAsync(Previous);
        (await Strings("SELECT table_name FROM information_schema.tables WHERE table_name = 'LoginThrottles'")).ShouldBeEmpty();
        (await Strings("SELECT indexname FROM pg_indexes WHERE indexname = 'IX_RecoveryThrottles_NextAllowedAt'")).ShouldBeEmpty();
        (await db.RecoveryThrottles.CountAsync(t => t.Id == recoveryId)).ShouldBe(1);
    }

    private async Task<List<string>> Strings(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }
}
