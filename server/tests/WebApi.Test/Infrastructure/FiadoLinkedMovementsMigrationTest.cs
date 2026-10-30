using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M7.7 Phase 5.6/5.7 upgrade-path proof for <c>M7_7Phase5FiadoLinkedMovements</c>: every existing branch gains the
/// two system-owned counterpart types in their own categories, exactly once; no existing type becomes a repayment
/// method by guess; and no historical transaction gains a generation origin.
/// </summary>
public class FiadoLinkedMovementsMigrationTest : IAsyncLifetime
{
    private const string PreLinkedMovementsMigration = "20261016225322_M7_7Phase76CloseReviewEvents";

    private static readonly Guid BranchA = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid BranchB = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");
    private static readonly Guid UserId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000003");
    private static readonly Guid TabId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000004");
    private static readonly Guid OperatorId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000005");
    private static readonly Guid CategoryId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000006");
    private static readonly Guid RepaymentTypeId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000007");
    private static readonly Guid LegacyRepaymentId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000008");
    private static readonly Guid ManagerActiveCategoryId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000009");
    private static readonly Guid ManagerInactiveCategoryId = Guid.Parse("eeeeeeee-0000-0000-0000-00000000000a");
    private static readonly Guid ManagerTypeId = Guid.Parse("eeeeeeee-0000-0000-0000-00000000000b");

    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("loto_fiado_linked_test")
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
    public async Task LinkedMovementsMigration_ShouldSeedSystemTypesPerBranch_WithoutInventingOriginOrMethods()
    {
        var options = new DbContextOptionsBuilder<ServerDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PreLinkedMovementsMigration);

            // Two legacy branches; one has a legacy "Cliente" repayment type (In, Tab + client) and a Tab In row.
            await dbContext.Database.ExecuteSqlRawAsync(
                $"""
                INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active")
                VALUES ('{BranchA}', 'Filial legada A', now(), TRUE),
                       ('{BranchB}', 'Filial legada B', now(), FALSE);

                INSERT INTO "Users" ("Id", "Name", "Email", "Password", "CreatedAt", "Active")
                VALUES ('{UserId}', 'Gerente legado', 'legacy-fiado@example.invalid', 'x', now(), TRUE);

                INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "TabAccountId", "CreatedAt", "Active")
                VALUES ('{TabId}', {(int)AccountType.Tab}, 'Fiado legado', '{BranchA}', NULL, now(), TRUE);

                INSERT INTO "Operators" ("Id", "Name", "BranchId", "UserId", "CreatedAt", "Active")
                VALUES ('{OperatorId}', 'Operador legado', '{BranchA}', NULL, now(), TRUE);

                INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "CreatedAt", "Active")
                VALUES ('{CategoryId}', 'Entradas', {(int)Direction.In}, '{BranchA}', now(), TRUE);

                INSERT INTO "TransactionTypes"
                    ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "CreatedAt", "Active")
                VALUES ('{RepaymentTypeId}', 'Cliente', 0, TRUE, '{CategoryId}', now(), TRUE);

                INSERT INTO "Transactions" (
                    "Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId", "DueDate",
                    "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "CreatedAt", "Active")
                VALUES (
                    '{LegacyRepaymentId}', DATE '2026-09-10', 50, '{RepaymentTypeId}', '{CategoryId}', {(short)Direction.In},
                    '{TabId}', DATE '2026-09-10', '{OperatorId}', '{UserId}', {(short)TransactionStatus.Active},
                    '{BranchA}', now(), TRUE);

                -- Existing configuration that collides with the default system names: an ACTIVE manager category
                -- (with its own type) in branch A and an INACTIVE one in branch B. Both must survive up and down.
                INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "CreatedAt", "Active")
                VALUES ('{ManagerActiveCategoryId}', 'Movimentos automáticos de fiado (entrada)', {(int)Direction.In},
                        '{BranchA}', now(), TRUE),
                       ('{ManagerInactiveCategoryId}', 'Movimentos automáticos de fiado (saída)', {(int)Direction.Out},
                        '{BranchB}', now(), FALSE);

                INSERT INTO "TransactionTypes"
                    ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "CreatedAt", "Active")
                VALUES ('{ManagerTypeId}', 'Tipo do gerente', 0, FALSE, '{ManagerActiveCategoryId}', now(), TRUE);
                """);
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync("20261017031934_M7_7Phase5FiadoLinkedMovements");
        }

        await using (var dbContext = new ServerDbContext(options))
        {
            foreach (var branchId in new[] { BranchA, BranchB })
            {
                // Projected reads: this schema predates later TransactionType columns (M11), so the current entity
                // shape cannot be materialized here.
                var systemTypes = await dbContext.TransactionTypes
                    .AsNoTracking()
                    .Where(type => type.Category.BranchId == branchId && type.SystemRole != null)
                    .Select(type => new
                    {
                        type.Name,
                        type.SystemRole,
                        type.Active,
                        type.FiadoPaymentKind,
                        type.RequiresTabAccountAndClient,
                        Category = new { type.Category.DefaultDirection }
                    })
                    .ToListAsync();

                systemTypes.Count.ShouldBe(2);
                var stock = systemTypes.Single(type => type.SystemRole == TransactionTypeSystemRole.FiadoCountedStockSaleCounterpart);
                var cash = systemTypes.Single(type => type.SystemRole == TransactionTypeSystemRole.FiadoCashRepaymentCounterpart);
                stock.Name.ShouldBe("Saída de estoque contado — venda fiado");
                stock.Category.DefaultDirection.ShouldBe(Direction.Out);
                cash.Name.ShouldBe("Entrada de dinheiro — recebimento fiado");
                cash.Category.DefaultDirection.ShouldBe(Direction.In);
                systemTypes.ShouldAllBe(type => type.Active && type.FiadoPaymentKind == null && !type.RequiresTabAccountAndClient);
            }

            var legacyType = await dbContext.TransactionTypes.AsNoTracking()
                .Where(type => type.Id == RepaymentTypeId)
                .Select(type => new { type.FiadoPaymentKind, type.SystemRole })
                .SingleAsync();
            legacyType.FiadoPaymentKind.ShouldBeNull();
            legacyType.SystemRole.ShouldBeNull();
            // Projected: the current Transaction entity carries columns this schema predates.
            var legacyRow = await dbContext.Transactions.AsNoTracking()
                .Where(row => row.Id == LegacyRepaymentId)
                .Select(row => new { row.GenerationKind, row.GeneratedFromTransactionId })
                .SingleAsync();
            legacyRow.GenerationKind.ShouldBeNull();
            legacyRow.GeneratedFromTransactionId.ShouldBeNull();
            (await dbContext.Transactions.CountAsync(row => row.GenerationKind != null)).ShouldBe(0);

            // The colliding active category is not adopted: branch A's system cash type lives in a new, disambiguated
            // category, and the manager's category keeps its name, state and only its own type.
            var cashCategory = await dbContext.TransactionTypes.AsNoTracking()
                .Where(type => type.SystemRole == TransactionTypeSystemRole.FiadoCashRepaymentCounterpart && type.Category.BranchId == BranchA)
                .Select(type => new { type.Category.Id, type.Category.Name })
                .SingleAsync();
            cashCategory.Id.ShouldNotBe(ManagerActiveCategoryId);
            cashCategory.Name.ShouldStartWith("Movimentos automáticos de fiado (entrada) — sistema ");
            var managerCategory = await dbContext.Categories.AsNoTracking().SingleAsync(category => category.Id == ManagerActiveCategoryId);
            managerCategory.Name.ShouldBe("Movimentos automáticos de fiado (entrada)");
            managerCategory.Active.ShouldBeTrue();
            (await dbContext.TransactionTypes.AsNoTracking().Where(type => type.CategoryId == ManagerActiveCategoryId)
                .Select(type => type.Id).ToListAsync()).ShouldBe([ManagerTypeId]);
            // An inactive same-name category does not collide with the active-name index: branch B keeps the default name.
            (await dbContext.TransactionTypes.AsNoTracking()
                .Where(type => type.SystemRole == TransactionTypeSystemRole.FiadoCountedStockSaleCounterpart && type.Category.BranchId == BranchB)
                .Select(type => type.Category.Name)
                .SingleAsync()).ShouldBe("Movimentos automáticos de fiado (saída)");
        }

        // Downgrade removes only migration-owned rows; every pre-existing category and type survives.
        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync(PreLinkedMovementsMigration);

            var categories = await dbContext.Database
                .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"Categories\"")
                .ToListAsync();
            categories.Order().ShouldBe(new[] { CategoryId, ManagerActiveCategoryId, ManagerInactiveCategoryId }.Order());
            var types = await dbContext.Database
                .SqlQuery<Guid>($"SELECT \"Id\" AS \"Value\" FROM \"TransactionTypes\"")
                .ToListAsync();
            types.Order().ShouldBe(new[] { RepaymentTypeId, ManagerTypeId }.Order());
        }

        // Upgrading again re-seeds exactly one pair per branch.
        await using (var dbContext = new ServerDbContext(options))
        {
            await dbContext.GetService<IMigrator>().MigrateAsync("20261017031934_M7_7Phase5FiadoLinkedMovements");
            (await dbContext.TransactionTypes.CountAsync(type => type.SystemRole != null)).ShouldBe(4);
        }
    }
}
