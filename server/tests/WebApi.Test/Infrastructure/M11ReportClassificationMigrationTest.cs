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
/// M11 Phases 4 and 6 upgrade on a real PostgreSQL database: existing types — even ones whose name, category and
/// settlement rule look like the seeded card, cheque, Tarifa Bolão and Sobra types — keep both report classifications
/// <c>None</c>; the database guards keep system types unclassified; no pending model change remains.
/// </summary>
public sealed class M11ReportClassificationMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261020104851_M95OpeningSideInflows";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m11_classification").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldLeaveEveryExistingTypeUnclassified_WithoutNameMatching()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var branch = new Branch { Name = "Existing branch" };
        var saidas = new Category { BranchId = branch.Id, Name = "Saídas", DefaultDirection = Direction.Out };
        var receita = new Category { BranchId = branch.Id, Name = "Receita", DefaultDirection = Direction.In };
        var comerciais = new Category { BranchId = branch.Id, Name = "Despesas Comerciais", DefaultDirection = Direction.Out };
        db.AddRange(branch, saidas, receita, comerciais);
        await db.SaveChangesAsync();
        await InsertPriorSchemaType(db, receita.Id, "Tarifa Bolão", SettlementRule.SameDay, true);
        await InsertPriorSchemaType(db, comerciais.Id, "Sobra de Bolão", SettlementRule.SameDay, true);
        await InsertPriorSchemaType(db, comerciais.Id, "Sobra de Federal", SettlementRule.SameDay, false);
        // Look-alikes of the seeded card and cheque types, written with the prior schema's columns.
        var cheque = await InsertPriorSchemaType(db, saidas.Id, "Depósito Cheque", SettlementRule.OperatorEnteredCheque, true);
        await InsertPriorSchemaType(db, saidas.Id, "Cartão de Crédito", SettlementRule.TwoBusinessDays, true);
        var retiredDebit = await InsertPriorSchemaType(db, saidas.Id, "Cartão de Débito", SettlementRule.NextBusinessDay, false);

        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();

        var types = await db.TransactionTypes.AsNoTracking().ToListAsync();
        types.Count.ShouldBe(6);
        types.ShouldAllBe(type => type.SettlementInstrument == SettlementInstrument.None);
        types.ShouldAllBe(type => type.MonthlyFinancialRole == MonthlyFinancialRole.None);
        types.Single(type => type.Id == cheque).SettlementRule.ShouldBe(SettlementRule.OperatorEnteredCheque);
        types.Single(type => type.Id == retiredDebit).Active.ShouldBeFalse();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

        // Both columns are guarded for system-owned types at the database level.
        var systemCategory = new Category { BranchId = branch.Id, Name = "Movimentos automáticos", DefaultDirection = Direction.In };
        db.Add(systemCategory);
        await db.SaveChangesAsync();
        foreach (var column in new[] { "SettlementInstrument", "MonthlyFinancialRole" })
        {
            var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync($"""
                INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "SystemRole",
                    "FiadoPaymentKind", "CategoryId", "CreatedAt", "Active", "SettlementInstrument", "MonthlyFinancialRole")
                VALUES ('{Guid.NewGuid()}', 'System {column}', 0, false, 2, NULL, '{systemCategory.Id}', now(), true,
                    {(column == "SettlementInstrument" ? 1 : 0)}, {(column == "MonthlyFinancialRole" ? 1 : 0)})
                """));
            error.ConstraintName.ShouldBe($"CK_TransactionTypes_SystemRole_No{column}");
        }
    }

    private static async Task<Guid> InsertPriorSchemaType(ServerDbContext db, Guid categoryId, string name, SettlementRule rule, bool active)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "SystemRole",
                "FiadoPaymentKind", "CategoryId", "CreatedAt", "Active")
            VALUES ({id}, {name}, {(short)rule}, {false}, {(short?)null}, {(short?)null}, {categoryId}, {DateTime.UtcNow}, {active})
            """);
        return id;
    }
}
