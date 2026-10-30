using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M7.7 Phase 7.6.1 upgrade-path proof. A close that existed before review events shipped is
/// migrated as having incomplete history and receives no fabricated events, while a close created
/// afterwards starts complete.
/// </summary>
public class CloseReviewEventsMigrationTest : IAsyncLifetime
{
    private const string PreEventsMigration = "20261016222112_M7_7Phase5StalePairingCleanup";

    private static readonly Guid BranchId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid UserId = Guid.Parse("dddddddd-0000-0000-0000-000000000002");
    private static readonly Guid AccountId = Guid.Parse("dddddddd-0000-0000-0000-000000000003");
    private static readonly Guid LegacyCloseId = Guid.Parse("dddddddd-0000-0000-0000-000000000004");

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("loto_review_events_test")
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
    public async Task EventsMigration_ShouldMarkExistingClosesIncomplete_WithoutFabricatingEvents()
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PreEventsMigration);

            // A legacy rejected close: its rejection time and actor were never captured.
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active")
                VALUES ('{BranchId}', 'Legacy Review Branch', now(), TRUE);

                INSERT INTO "Users" ("Id", "Name", "Email", "Password", "CreatedAt", "Active")
                VALUES ('{UserId}', 'Gerente legado', 'legacy-review@example.invalid', 'x', now(), TRUE);

                INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "TabAccountId", "CreatedAt", "Active")
                VALUES ('{AccountId}', 0, 'Terminal legado', '{BranchId}', NULL, now(), TRUE);

                INSERT INTO "DailyCloses" (
                    "Id", "Date", "Status", "AccountId", "OpenedByUserId", "RejectionReason",
                    "BranchId", "CreatedAt", "Active")
                VALUES (
                    '{LegacyCloseId}', DATE '2026-09-10', {(short)DailyCloseStatus.Rejected}, '{AccountId}', '{UserId}',
                    'Rejeitado antes dos eventos', '{BranchId}', now(), TRUE);
                """);
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync("20261016225322_M7_7Phase76CloseReviewEvents");
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            // Projected and written with this schema's columns: the current DailyClose entity carries session ones.
            var legacy = await dbContext.DailyCloses.AsNoTracking()
                .Where(close => close.Id == LegacyCloseId)
                .Select(close => new { close.ReviewHistoryComplete, close.RejectionReason })
                .SingleAsync();
            legacy.ReviewHistoryComplete.ShouldBeFalse();
            legacy.RejectionReason.ShouldBe("Rejeitado antes dos eventos");
            (await dbContext.DailyCloseReviewEvents.CountAsync()).ShouldBe(0);

            // A close written after the migration carries the entity's complete-history default.
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "DailyCloses" ("Id", "Date", "Status", "AccountId", "OpenedByUserId", "BranchId", "CreatedAt",
                    "Active", "ReviewHistoryComplete")
                VALUES ({Guid.NewGuid()}, {new DateOnly(2026, 9, 11)}, 0, {AccountId}, {UserId}, {BranchId}, now(), true,
                    {new DailyClose().ReviewHistoryComplete})
                """);
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            (await dbContext.DailyCloses.AsNoTracking()
                    .Where(close => close.Date == new DateTime(2026, 9, 11))
                    .Select(close => close.ReviewHistoryComplete)
                    .SingleAsync())
                .ShouldBeTrue();
        }
    }
}
