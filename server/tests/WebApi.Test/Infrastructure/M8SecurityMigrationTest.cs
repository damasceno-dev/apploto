using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

public sealed class M8SecurityMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261017031934_M7_7Phase5FiadoLinkedMovements";
    private const string BeforeOperatedAndGoogle = "20261018040013_M8AccountSecurityAndInvitations";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
         .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m8_upgrade").WithUsername("postgres").WithPassword("postgres").Build();
    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldNormalizeLegacyEmail_BackfillStableSessions_AndAddOnlyHashCapabilityStorage()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var userId = Guid.NewGuid();
        var refreshId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\", \"Name\", \"Email\", \"Password\", \"CreatedAt\", \"Active\") VALUES ({userId}, 'Legacy', '\u00A0LEGACY@EXAMPLE.INVALID\u202F', 'legacy-hash', now(), TRUE)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"RefreshTokens\" (\"Id\", \"UserId\", \"Value\", \"CreatedAt\", \"Active\") VALUES ({refreshId}, {userId}, 'legacy-refresh', now(), TRUE)");
        await db.GetService<IMigrator>().MigrateAsync("20261018165353_M8OperatedOnboardingAndGoogleIdentity");
        var migratedUser = await db.Users.SingleAsync(u => u.Id == userId);
        migratedUser.Email.ShouldBe("legacy@example.invalid");
        migratedUser.IsPlatformOperator.ShouldBeFalse();
        migratedUser.EmailVerifiedAt.ShouldBeNull();
        var refresh = await db.RefreshTokens.SingleAsync(t => t.Id == refreshId);
        refresh.SessionId.ShouldBe(refreshId);
        refresh.Value.ShouldBe("legacy-refresh");
        refresh.Active.ShouldBeTrue();
        (await db.PasswordResetTokens.CountAsync()).ShouldBe(0);
        (await db.Invitations.CountAsync()).ShouldBe(0);
        (await db.OperatorLinkRequests.CountAsync()).ShouldBe(0);
        (await db.OperatedOnboardings.CountAsync()).ShouldBe(0);
        (await db.GoogleIdentities.CountAsync()).ShouldBe(0);
        (await db.GoogleLoginChallenges.CountAsync()).ShouldBe(0);
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var columns = new NpgsqlCommand("SELECT column_name FROM information_schema.columns WHERE table_name IN ('Invitations','PasswordResetTokens')", connection);
        await using var reader = await columns.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        names.ShouldContain("TokenHash");
        names.ShouldNotContain("Token");
        names.ShouldNotContain("Capability");
    }

    [Fact]
    public async Task CollisionPreflight_ShouldRollbackWithoutPickingOrRewritingAnAccount()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Users\" (\"Id\", \"Name\", \"Email\", \"Password\", \"CreatedAt\", \"Active\") VALUES ({first}, 'First', 'COLLISION@example.invalid', 'hash1', now(), TRUE), ({second}, 'Second', 'collision@example.invalid', 'hash2', now(), TRUE)");
        var error = await Should.ThrowAsync<PostgresException>(() => db.Database.MigrateAsync());
        error.MessageText.ShouldBe("M8_EMAIL_NORMALIZATION_COLLISION");
        // The failed M8 migration leaves the database at the previous schema. Query only
        // columns that existed there; the current User model also has later M8 fields.
        (await db.Database.SqlQuery<string>($"SELECT \"Email\" AS \"Value\" FROM \"Users\" WHERE \"Id\" = {first}").SingleAsync())
            .ShouldBe("COLLISION@example.invalid");
        (await db.Database.GetPendingMigrationsAsync()).ShouldContain(m => m.EndsWith("M8AccountSecurityAndInvitations"));
    }

    [Fact]
    public async Task PhaseSixSevenDown_ShouldDropOnlyItsNewSchemaObjects()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync("20261018165353_M8OperatedOnboardingAndGoogleIdentity");
        await db.GetService<IMigrator>().MigrateAsync(BeforeOperatedAndGoogle);
        var newTables = await db.Database.SqlQuery<long>($"""
            SELECT COUNT(*)::bigint AS "Value" FROM information_schema.tables
            WHERE table_schema = 'public' AND table_name IN ('OperatedOnboardings', 'GoogleIdentities', 'GoogleLoginChallenges')
            """).SingleAsync();
        newTables.ShouldBe(0);
        var newColumns = await db.Database.SqlQuery<long>($"""
            SELECT COUNT(*)::bigint AS "Value" FROM information_schema.columns
            WHERE table_name = 'Users' AND column_name IN ('IsPlatformOperator', 'EmailVerifiedAt')
            """).SingleAsync();
        newColumns.ShouldBe(0);
        (await db.Database.GetPendingMigrationsAsync()).ShouldContain(m => m.EndsWith("M8OperatedOnboardingAndGoogleIdentity"));
    }
}
