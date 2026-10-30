using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M7.7 Phase 5.3 upgrade-path proof. Builds the exact pre-cleanup schema
/// (<c>M7_7Phase7TimeEntryPolicy</c>) in its own short-lived PostgreSQL container, seeds every
/// stale pairing shape the old deactivation could leave behind next to a valid pair, then applies
/// <c>M7_7Phase5StalePairingCleanup</c> and asserts only the stale pairings were cleared.
/// </summary>
public class AccountPairingCleanupMigrationTest : IAsyncLifetime
{
    private const string PreCleanupMigration = "20260823204707_M7_7Phase7TimeEntryPolicy";
    private const int Terminal = 0;
    private const int Tab = 2;

    private static readonly Guid BranchId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid ValidTerminalId = Guid.Parse("cccccccc-0000-0000-0000-000000000011");
    private static readonly Guid ValidTabId = Guid.Parse("cccccccc-0000-0000-0000-000000000012");
    private static readonly Guid InactiveTerminalId = Guid.Parse("cccccccc-0000-0000-0000-000000000021");
    private static readonly Guid ReservedTabId = Guid.Parse("cccccccc-0000-0000-0000-000000000022");
    private static readonly Guid ActiveTerminalId = Guid.Parse("cccccccc-0000-0000-0000-000000000031");
    private static readonly Guid InactiveTabId = Guid.Parse("cccccccc-0000-0000-0000-000000000032");
    private static readonly Guid BothInactiveTerminalId = Guid.Parse("cccccccc-0000-0000-0000-000000000041");
    private static readonly Guid BothInactiveTabId = Guid.Parse("cccccccc-0000-0000-0000-000000000042");
    private static readonly Guid SpareTerminalId = Guid.Parse("cccccccc-0000-0000-0000-000000000051");

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("loto_pairing_cleanup_test")
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
    public async Task CleanupMigration_ShouldClearOnlyStalePairings_AndFreeReservedTabs()
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PreCleanupMigration);

            // Tabs first (the FK points from a Terminal to its Tab), then the Terminals holding them.
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active")
                VALUES ('{BranchId}', 'Legacy Pairing Branch', now(), TRUE);

                INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "TabAccountId", "CreatedAt", "Active")
                VALUES
                    ('{ValidTabId}', {Tab}, 'Fiado valido', '{BranchId}', NULL, now(), TRUE),
                    ('{ReservedTabId}', {Tab}, 'Fiado reservado', '{BranchId}', NULL, now(), TRUE),
                    ('{InactiveTabId}', {Tab}, 'Fiado inativo', '{BranchId}', NULL, now(), FALSE),
                    ('{BothInactiveTabId}', {Tab}, 'Fiado inativo 2', '{BranchId}', NULL, now(), FALSE);

                INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "TabAccountId", "CreatedAt", "Active")
                VALUES
                    ('{ValidTerminalId}', {Terminal}, 'Terminal valido', '{BranchId}', '{ValidTabId}', now(), TRUE),
                    ('{InactiveTerminalId}', {Terminal}, 'Terminal inativo', '{BranchId}', '{ReservedTabId}', now(), FALSE),
                    ('{ActiveTerminalId}', {Terminal}, 'Terminal ativo', '{BranchId}', '{InactiveTabId}', now(), TRUE),
                    ('{BothInactiveTerminalId}', {Terminal}, 'Terminal inativo 2', '{BranchId}', '{BothInactiveTabId}', now(), FALSE),
                    ('{SpareTerminalId}', {Terminal}, 'Terminal livre', '{BranchId}', NULL, now(), TRUE);
                """);
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync("20261016222112_M7_7Phase5StalePairingCleanup");
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            var pairings = await dbContext.Accounts
                .AsNoTracking()
                .Where(account => account.BranchId == BranchId)
                .Select(account => new { account.Id, account.TabAccountId })
                .ToDictionaryAsync(account => account.Id, account => account.TabAccountId);

            pairings[ValidTerminalId].ShouldBe(ValidTabId);
            pairings[InactiveTerminalId].ShouldBeNull();
            pairings[ActiveTerminalId].ShouldBeNull();
            pairings[BothInactiveTerminalId].ShouldBeNull();
            pairings[SpareTerminalId].ShouldBeNull();

            // The Tab the inactive Terminal used to reserve now pairs without hitting the unique index.
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""UPDATE "Accounts" SET "TabAccountId" = '{ReservedTabId}' WHERE "Id" = '{SpareTerminalId}';""");
            // Projected: the current Account entity carries columns this schema predates.
            (await dbContext.Accounts.AsNoTracking()
                    .Where(account => account.Id == SpareTerminalId)
                    .Select(account => account.TabAccountId)
                    .SingleAsync())
                .ShouldBe(ReservedTabId);
        }
    }
}
