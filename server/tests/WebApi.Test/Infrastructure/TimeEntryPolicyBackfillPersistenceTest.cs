using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.TimeEntries;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M7.7 Phase 7 upgrade-path proof. Uses its own short-lived PostgreSQL container to
/// build the exact pre-Phase-7 schema (target migration <c>M7_7Phase6Contracts</c>),
/// seeds legacy branches/settings/entries the way a production database would hold
/// them, then applies <c>M7_7Phase7TimeEntryPolicy</c> and asserts the deterministic
/// backfill: one MinValue-dated initial policy row per branch, mirroring that branch's
/// own Setting constants, under which every persisted balance recomputes unchanged.
/// </summary>
public class TimeEntryPolicyBackfillPersistenceTest : IAsyncLifetime
{
    private const string PrePhase7Migration = "20260820084337_M7_7Phase6Contracts";

    private static readonly Guid BranchAlfaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BranchBetaId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid OperatorAlfaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid EntryAlfaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");
    private static readonly Guid EntryAlfaLowerTierId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000004");

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("loto_backfill_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task Phase7Migration_ShouldBackfillOneInitialPolicyPerBranch_PreservingPersistedBalances()
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        // 1. Pre-Phase-7 world: schema stops right before the policy table exists, and two
        // branches carry DIFFERENT constants plus two closed worked entries with their
        // persisted checkpoints under Alfa's 7.33/1.00/0.25 constants: a nine-gross-hour
        // shift (over-6h tier: 8 h, +0.67) and a five-gross-hour shift (over-4h tier:
        // 4.75 h, −2.58), so both lunch tiers participate in balance preservation.
        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PrePhase7Migration);

            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active")
                VALUES
                    ('{BranchAlfaId}', 'Legacy Alfa', now(), TRUE),
                    ('{BranchBetaId}', 'Legacy Beta', now(), TRUE);

                INSERT INTO "Settings" (
                    "Id", "LockDate", "DailyTargetHours", "LunchDeductionOver6H",
                    "LunchDeductionOver4H", "BranchId", "CreatedAt", "Active")
                VALUES
                    (gen_random_uuid(), DATE '0001-01-01', 7.33, 1.00, 0.25, '{BranchAlfaId}', now(), TRUE),
                    (gen_random_uuid(), DATE '0001-01-01', 8.00, 2.00, 0.50, '{BranchBetaId}', now(), TRUE);

                INSERT INTO "Operators" ("Id", "Name", "BranchId", "UserId", "CreatedAt", "Active")
                VALUES ('{OperatorAlfaId}', 'Legacy Operadora', '{BranchAlfaId}', NULL, now(), TRUE);

                INSERT INTO "TimeEntries" (
                    "Id", "Date", "Status", "TotalHours", "BalanceHours",
                    "OperatorId", "BranchId", "CreatedAt", "Active")
                VALUES
                    ('{EntryAlfaId}', DATE '2026-05-04', 1, 8.00, 0.67,
                     '{OperatorAlfaId}', '{BranchAlfaId}', now(), TRUE),
                    ('{EntryAlfaLowerTierId}', DATE '2026-05-05', 1, 4.75, -2.58,
                     '{OperatorAlfaId}', '{BranchAlfaId}', now(), TRUE);

                INSERT INTO "TimeEntrySegments" ("Id", "TimeEntryId", "ClockIn", "ClockOut", "CreatedAt", "Active")
                VALUES
                    (gen_random_uuid(), '{EntryAlfaId}',
                     TIMESTAMP '2026-05-04 08:00:00', TIMESTAMP '2026-05-04 17:00:00', now(), TRUE),
                    (gen_random_uuid(), '{EntryAlfaLowerTierId}',
                     TIMESTAMP '2026-05-05 08:00:00', TIMESTAMP '2026-05-05 13:00:00', now(), TRUE);
                """);
        }

        // 2. Upgrade: apply the remaining migrations (Phase 7 included) on top of the data.
        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.Database.MigrateAsync();
            (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

            // Deterministic backfill: exactly one MinValue-dated row per branch, each
            // mirroring its OWN branch's constants.
            var policies = await dbContext.TimeEntryPolicies
                .AsNoTracking()
                .OrderBy(policy => policy.BranchId)
                .ToListAsync();

            policies.Count.ShouldBe(2);
            policies.ShouldAllBe(policy => policy.EffectiveFrom == DateTime.MinValue);
            policies.ShouldAllBe(policy => policy.Active);

            var alfaPolicy = policies.Single(policy => policy.BranchId == BranchAlfaId);
            alfaPolicy.DailyTargetHours.ShouldBe(7.33m);
            alfaPolicy.LunchDeductionOver6H.ShouldBe(1.00m);
            alfaPolicy.LunchDeductionOver4H.ShouldBe(0.25m);

            var betaPolicy = policies.Single(policy => policy.BranchId == BranchBetaId);
            betaPolicy.DailyTargetHours.ShouldBe(8.00m);
            betaPolicy.LunchDeductionOver6H.ShouldBe(2.00m);
            betaPolicy.LunchDeductionOver4H.ShouldBe(0.50m);

            // Balance preservation: recomputing each legacy entry under its resolved policy
            // reproduces the persisted pre-migration checkpoint exactly — for the over-6h
            // lunch tier (9 h gross → 8.00 / +0.67) AND the over-4h tier
            // (5 h gross → 4.75 / −2.58), so every backfilled constant is exercised.
            var alfaPolicies = policies.Where(policy => policy.BranchId == BranchAlfaId).ToList();
            foreach (var (entryId, expectedTotal, expectedBalance) in new[]
                     {
                         (EntryAlfaId, 8.00m, 0.67m),
                         (EntryAlfaLowerTierId, 4.75m, -2.58m)
                     })
            {
                var entry = await dbContext.TimeEntries
                    .AsNoTracking()
                    .Include(te => te.Segments)
                    .SingleAsync(te => te.Id == entryId);
                entry.TotalHours.ShouldBe(expectedTotal);
                entry.BalanceHours.ShouldBe(expectedBalance);

                var resolved = TimeEntryPolicyResolver.Resolve(alfaPolicies, entry.Date);
                var (totalHours, balanceHours) = new TimeEntryCalculationService().Calculate(
                    entry.Status,
                    entry.Segments
                        .Where(segment => segment.Active)
                        .Select(segment => new TimeEntrySegmentInput(segment.ClockIn, segment.ClockOut))
                        .ToList(),
                    entry.Date,
                    branchLocalNow: new DateTime(2026, 5, 10, 12, 0, 0),
                    resolved.DailyTargetHours,
                    resolved.LunchDeductionOver6H,
                    resolved.LunchDeductionOver4H);

                totalHours.ShouldBe(entry.TotalHours);
                balanceHours.ShouldBe(entry.BalanceHours);
            }
        }
    }

    [Fact]
    public async Task Phase7Migration_ShouldAbortLoudly_WhenABranchHasNoSettingRow()
    {
        var orphanBranchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var pairedBranchId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");

        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        // Pre-Phase-7 world with a legacy/imported/partially seeded branch that has NO
        // Setting row at all — the relationship is not database-enforced (Branch.Setting
        // is nullable) — alongside one normally paired branch.
        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PrePhase7Migration);

            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active")
                VALUES
                    ('{orphanBranchId}', 'Orphan (no Setting)', now(), TRUE),
                    ('{pairedBranchId}', 'Paired', now(), TRUE);

                INSERT INTO "Settings" (
                    "Id", "LockDate", "DailyTargetHours", "LunchDeductionOver6H",
                    "LunchDeductionOver4H", "BranchId", "CreatedAt", "Active")
                VALUES
                    (gen_random_uuid(), DATE '0001-01-01', 7.33, 1.00, 0.25, '{pairedBranchId}', now(), TRUE);
                """);
        }

        // The migration must fail loudly rather than silently completing with the orphan
        // branch left at zero policy rows.
        await using (var dbContext = new ServerDbContext(options))
        {
            await Should.ThrowAsync<Exception>(() => dbContext.Database.MigrateAsync());
        }

        // Whole-migration transaction rolls back: the Phase 7 migration (and therefore the
        // TimeEntryPolicies table) never committed, for the orphan OR the paired branch.
        await using (var dbContext = new ServerDbContext(options))
        {
            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
            pendingMigrations.ShouldContain("20260823204707_M7_7Phase7TimeEntryPolicy");
        }
    }
}

/// <summary>
/// Schema assertions for the Phase 7 table against the shared clean-migration database.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TimeEntryPolicySchemaPersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task MigratedDatabase_ShouldContainPolicyTableWithFilteredUniqueIndexAndRestrictedForeignKey()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await dbContext.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();

        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_name = 'TimeEntryPolicies'
              AND column_name IN (
                  'Id',
                  'EffectiveFrom',
                  'DailyTargetHours',
                  'LunchDeductionOver6H',
                  'LunchDeductionOver4H',
                  'BranchId',
                  'CreatedAt',
                  'Active')
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(8);

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE tablename = 'TimeEntryPolicies'
              AND indexname = 'IX_TimeEntryPolicies_BranchId_EffectiveFrom'
              AND indexdef LIKE '%UNIQUE%'
              AND indexdef LIKE '%WHERE%'
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(1);

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conrelid = '"TimeEntryPolicies"'::regclass
              AND contype = 'f'
              AND confdeltype = 'r'
              AND conname = 'FK_TimeEntryPolicies_Branches_BranchId'
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(1);
    }
}
