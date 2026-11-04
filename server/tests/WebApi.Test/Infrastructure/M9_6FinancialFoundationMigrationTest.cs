using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M9.6 Phases 6, 7 and 9 upgrade from the Phase 4 schema on its own PostgreSQL: without a reviewed map an existing
/// branch is left exactly as it was (only the system fiado-sale label changes); with the map the seeds apply during
/// the migration; and once the financial foundation holds data the downgrade refuses instead of losing history.
/// Rows are written with the previous schema's own columns.
/// </summary>
public sealed class M9_6FinancialFoundationMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261030103901_M9_6Phase4BlindCountApproval";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m9_6_phase6").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldLeaveAnUnmappedBranchAsItWas_ThenApplyAReviewedMapOnce_AndRefuseTheDowngrade()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var legacy = await SeedPriorBranch();
        var before = await Strings($"""
            SELECT t."Id"::text || ':' || t."Name" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = '{legacy.BranchId}' AND t."SystemRole" IS NULL ORDER BY t."Id"
            """);

        await db.Database.MigrateAsync();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await Strings("SELECT proname::text FROM pg_proc WHERE proname = 'loto_m96_apply_financial_seeds'")).ShouldHaveSingleItem();
        (await Strings($"""
            SELECT t."Id"::text || ':' || t."Name" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = '{legacy.BranchId}' AND t."SystemRole" IS NULL ORDER BY t."Id"
            """)).ShouldBe(before);
        (await Strings($"""SELECT "Name" FROM "TransactionTypes" WHERE "Id" = '{legacy.FiadoSaleSystemTypeId}'"""))
            .ShouldBe(["Saída de caixa — venda fiado"]);
        (await Strings("""SELECT "Id"::text FROM "TransactionTypes" WHERE "SeedKey" IS NOT NULL""")).ShouldBeEmpty();

        // The reviewed map, applied after the upgrade, converts exactly the named identities.
        await using (var connection = new NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var apply = new NpgsqlCommand("SELECT count(*) FROM loto_m96_apply_financial_seeds(@map::jsonb)", connection);
            apply.Parameters.AddWithValue("map", legacy.Map);
            ((long)(await apply.ExecuteScalarAsync())!).ShouldBeGreaterThan(30);
        }
        (await Strings($"""SELECT "SystemRole"::text FROM "TransactionTypes" WHERE "Id" = '{legacy.TarifaId}'""")).ShouldBe(["5"]);
        (await Strings($"""SELECT "Name" FROM "TransactionTypes" WHERE "Id" = '{legacy.OperadorId}'""")).ShouldBe(["Relatório do terminal"]);
        (await Strings($"""SELECT "SystemRole"::text FROM "TransactionTypes" WHERE "Id" = '{legacy.DepositId}'""")).ShouldBe(["7"]);

        var refused = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(Previous));
        refused.MessageText.ShouldContain("M96_FORWARD_ONLY");
        (await Strings("SELECT table_name::text FROM information_schema.tables WHERE table_name = 'SessionFieldEffects'")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Upgrade_ShouldApplyTheReviewedMapDuringTheMigration_WhenTheOwnerSuppliesIt()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var legacy = await SeedPriorBranch();

        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('loto.m96_financial_seed_map', {legacy.Map}, false)");
        await db.Database.MigrateAsync();

        var roles = await Strings($"""
            SELECT t."SystemRole"::text FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = '{legacy.BranchId}' AND t."SystemRole" IS NOT NULL ORDER BY t."SystemRole"
            """);
        roles.ShouldBe(["1", "3", "4", "5", "6", "7", "8", "9"]);
        (await Strings($"""
            SELECT t."Name" FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = '{legacy.BranchId}' AND t."FiadoPaymentKind" IS NOT NULL ORDER BY t."Name"
            """)).ShouldBe(["Dinheiro", "PIX"]);
        (await Strings($"""
            SELECT count(*)::text FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
            WHERE c."BranchId" = '{legacy.BranchId}' AND c."Name" LIKE 'Despesas %' AND t."SeedKey" IS NOT NULL
            """)).ShouldBe(["32"]);
    }

    [Fact]
    public async Task EmptyDatabase_ShouldUpgradeAndDowngradeCleanly()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        await db.Database.MigrateAsync();
        (await Strings("SELECT table_name::text FROM information_schema.tables WHERE table_name IN ('SessionFieldEffects', 'CustodyTransfers') ORDER BY 1"))
            .ShouldBe(["CustodyTransfers", "SessionFieldEffects"]);

        await migrator.MigrateAsync(Previous);

        (await Strings("SELECT table_name::text FROM information_schema.tables WHERE table_name IN ('SessionFieldEffects', 'CustodyTransfers')")).ShouldBeEmpty();
        (await Strings("SELECT proname::text FROM pg_proc WHERE proname = 'loto_m96_apply_financial_seeds'")).ShouldBeEmpty();
        (await Strings("SELECT column_name::text FROM information_schema.columns WHERE table_name = 'TransactionTypes' AND column_name = 'SeedKey'")).ShouldBeEmpty();
    }

    private sealed record PriorBranch(Guid BranchId, Guid TarifaId, Guid OperadorId, Guid DepositId, Guid FiadoSaleSystemTypeId, string Map);

    /// <summary>A branch at the Phase 4 schema: the M9.5 catalog (no seed keys, no close-field roles).</summary>
    private async Task<PriorBranch> SeedPriorBranch()
    {
        var branchId = Guid.NewGuid();
        var categories = new Dictionary<string, (Guid Id, short Direction)>
        {
            ["Receita"] = (Guid.NewGuid(), 0),
            ["Entradas"] = (Guid.NewGuid(), 0),
            ["Saídas"] = (Guid.NewGuid(), 1),
            ["Despesas Administrativas"] = (Guid.NewGuid(), 1),
            ["Despesas Pessoal"] = (Guid.NewGuid(), 1),
            ["Despesas Financeiras"] = (Guid.NewGuid(), 1),
            ["Movimentos automáticos de fiado (saída)"] = (Guid.NewGuid(), 1)
        };
        var tarifa = Guid.NewGuid();
        var operador = Guid.NewGuid();
        var deposit = Guid.NewGuid();
        var fiadoSale = Guid.NewGuid();
        var values = string.Join(",\n", categories.Select(pair =>
            $"('{pair.Value.Id}', '{pair.Key}', {pair.Value.Direction}, '{branchId}', true, now())"));
        await Execute($"""
            INSERT INTO "Branches" ("Id", "Name", "CreatedAt", "Active") VALUES ('{branchId}', 'Lotérica legada', now(), true);
            INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "Active", "CreatedAt") VALUES
            {values};
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId",
                "Active", "CreatedAt", "SystemRole", "SettlementInstrument", "MonthlyFinancialRole") VALUES
            ('{tarifa}', 'Tarifa Bolão', 0, false, '{categories["Receita"].Id}', true, now(), NULL, 0, 1),
            ('{operador}', 'Operador', 0, false, '{categories["Entradas"].Id}', true, now(), NULL, 0, 0),
            ('{deposit}', 'Depósito Dinheiro', 1, false, '{categories["Saídas"].Id}', true, now(), NULL, 0, 0),
            ('{fiadoSale}', 'Saída de estoque contado — venda fiado', 0, false,
                '{categories["Movimentos automáticos de fiado (saída)"].Id}', true, now(), 1, 0, 0);
            """);
        var map = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object?>
            {
                ["branchId"] = branchId,
                ["receitaCategoryId"] = categories["Receita"].Id,
                ["entradasCategoryId"] = categories["Entradas"].Id,
                ["saidasCategoryId"] = categories["Saídas"].Id,
                ["despesasAdministrativasCategoryId"] = categories["Despesas Administrativas"].Id,
                ["despesasPessoalCategoryId"] = categories["Despesas Pessoal"].Id,
                ["despesasFinanceirasCategoryId"] = categories["Despesas Financeiras"].Id,
                ["bolaoFeeTypeId"] = tarifa,
                ["terminalReportTypeId"] = operador,
                ["safeDepositTypeId"] = deposit
            }
        });
        return new PriorBranch(branchId, tarifa, operador, deposit, fiadoSale, map);
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
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
