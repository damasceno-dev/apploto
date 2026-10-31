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
/// M9.6 Phase 4 upgrade on a real PostgreSQL database: every existing session receives its own confirmation token once,
/// without a count attempt or event, every revision starts at zero, and the downgrade drops the attempt tables cleanly.
/// Rows are written with the previous schema's own columns.
/// </summary>
public sealed class M9_6Phase4BlindCountMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261029122442_M9_6Phase2SessionCustody";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m9_6_phase4").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldGiveEachExistingSessionItsOwnToken_WithoutAttempts_AndDowngradeCleanly()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var branchId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        await InsertMinimal("Branches", new() { ["Id"] = branchId });
        await InsertMinimal("Users", new() { ["Id"] = userId });
        await InsertMinimal("Accounts", new() { ["Id"] = accountId, ["BranchId"] = branchId });
        var sessions = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var (sessionId, index) in sessions.Select((id, index) => (id, index)))
        {
            // Unresolved legacy sessions (quarantined by the Phase 2 upgrade) need no custody identity.
            await InsertMinimal("DailyCloses", new()
            {
                ["Id"] = sessionId,
                ["BranchId"] = branchId,
                ["AccountId"] = accountId,
                ["OpenedByUserId"] = userId,
                ["Date"] = new DateTime(2026, 9, 1 + index),
                ["MappingState"] = (short)1,
                ["CountRevision"] = 3L,
                ["LedgerRevision"] = 0L
            });
        }

        await db.Database.MigrateAsync();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        var rows = await db.DailyCloses.AsNoTracking().Where(close => close.BranchId == branchId).ToListAsync();
        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(close => close.CurrentConfirmationToken != Guid.Empty);
        rows.Select(close => close.CurrentConfirmationToken).Distinct().Count().ShouldBe(3);
        rows.ShouldAllBe(close => close.OpeningRevision == 0 && close.LastAttemptNumber == 0 && close.CountRevision == 3);
        rows.ShouldAllBe(close => close.SubmittedAttemptId == null && close.RecountStartedForAttemptId == null);
        (await db.SessionCountAttempts.CountAsync()).ShouldBe(0);
        (await db.SessionEvents.CountAsync()).ShouldBe(0);
        (await Strings("SELECT tgname::text FROM pg_trigger WHERE tgname LIKE 'TR_SessionCountAttempt%'"))
            .Order(StringComparer.Ordinal).ShouldBe(["TR_SessionCountAttemptItems_Immutable", "TR_SessionCountAttempts_Immutable"]);

        await migrator.MigrateAsync(Previous);
        (await Strings("SELECT table_name::text FROM information_schema.tables WHERE table_name LIKE 'SessionCountAttempt%'")).ShouldBeEmpty();
        (await Strings("SELECT column_name::text FROM information_schema.columns WHERE table_name = 'DailyCloses' AND column_name = 'CurrentConfirmationToken'")).ShouldBeEmpty();
        (await Strings($"SELECT \"Id\"::text FROM \"DailyCloses\" WHERE \"BranchId\" = '{branchId}'")).Count.ShouldBe(3);
    }

    /// <summary>Inserts a row giving every NOT NULL column without a default a neutral value of its type.</summary>
    private async Task InsertMinimal(string table, Dictionary<string, object> values)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        var columns = new List<(string Name, string Type)>();
        await using (var describe = new NpgsqlCommand(
                         "SELECT column_name, data_type FROM information_schema.columns WHERE table_name = @table AND is_nullable = 'NO' AND column_default IS NULL",
                         connection))
        {
            describe.Parameters.AddWithValue("table", table);
            await using var reader = await describe.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns.Add((reader.GetString(0), reader.GetString(1)));
        }

        var row = new Dictionary<string, object>(values);
        foreach (var (name, type) in columns.Where(column => row.ContainsKey(column.Name) is false))
        {
            row[name] = type switch
            {
                "uuid" => Guid.NewGuid(),
                "boolean" => name == "Active",
                "smallint" => (short)0,
                "integer" => 0,
                "bigint" => 0L,
                "numeric" => 0m,
                "date" => new DateTime(2026, 9, 1),
                "timestamp with time zone" => DateTime.UtcNow,
                "jsonb" => "{}",
                _ => $"{table}-{name}-{Guid.NewGuid():N}"[..Math.Min(40, table.Length + name.Length + 34)]
            };
        }

        var names = row.Keys.ToList();
        await using var insert = new NpgsqlCommand(
            $"INSERT INTO \"{table}\" ({string.Join(", ", names.Select(name => $"\"{name}\""))}) VALUES ({string.Join(", ", names.Select((_, index) => $"@p{index}"))})",
            connection);
        for (var index = 0; index < names.Count; index++)
        {
            var value = row[names[index]];
            insert.Parameters.Add(value is string json && json == "{}"
                ? new NpgsqlParameter($"p{index}", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = json }
                : new NpgsqlParameter($"p{index}", value));
        }

        await insert.ExecuteNonQueryAsync();
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
