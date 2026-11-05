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

public sealed class M9_7BankingMigrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(p =>
        {
            foreach (var bindings in p.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        }).WithDatabase("loto_banking_upgrade").WithUsername("postgres").WithPassword("postgres").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task UpgradeRetainsExistingPendingChequeReceiptChainAndFrozenOriginalWithoutNewMoney()
    {
        await using var db = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031020206_M9_6Phase6To9FinancialFoundation");
        var legacy = await M9_7SettlementEventsMigrationTest.SeedPriorSchemaAsync(db);
        await migrator.MigrateAsync("20261103130725_M9_7Phase1To2SettlementEvents");
        var original = await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == legacy.Unpaid);
        var receipt = new BankReceipt
        {
            BranchId = legacy.BranchId, BankAccountId = legacy.BankAccountId, SourceKind = BankReceiptSourceKind.ChequeDeposit,
            OccurredOn = new DateTime(2026, 9, 30), PostedOn = new DateTime(2026, 10, 1), Amount = 90m, CreatedByUserId = legacy.UserId
        };
        var deposit = new SettlementEvent
        {
            BranchId = legacy.BranchId, ReceivableTransactionId = legacy.Unpaid, Sequence = 1, Kind = SettlementEventKind.ChequeDeposited,
            Amount = 90m, OutstandingDelta = -90m, OccurredOn = receipt.OccurredOn, PostedOn = receipt.PostedOn,
            Provenance = SettlementEventProvenance.Command, SettlementInstrument = SettlementInstrument.Cheque,
            BankReceiptId = receipt.Id, CreatedByUserId = legacy.UserId
        };
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "BankReceipts" ("Id", "BranchId", "BankAccountId", "SourceKind", "Status", "OccurredOn", "PostedOn", "Amount", "CreatedByUserId", "CreatedAt", "Active")
                VALUES ({receipt.Id}, {legacy.BranchId}, {legacy.BankAccountId}, 1, 1, DATE '2026-09-30', DATE '2026-10-01', 90, {legacy.UserId}, now(), true);
                INSERT INTO "SettlementEvents" ("Id", "BranchId", "ReceivableTransactionId", "Sequence", "Kind", "Amount", "OutstandingDelta", "OccurredOn", "PostedOn", "Provenance", "MigrationIssue", "SettlementInstrument", "BankReceiptId", "CreatedByUserId", "CreatedAt", "Active")
                VALUES ({deposit.Id}, {legacy.BranchId}, {legacy.Unpaid}, 1, 3, 90, -90, DATE '2026-09-30', DATE '2026-10-01', 1, 0, 1, {receipt.Id}, {legacy.UserId}, now(), true);
                """);
            await transaction.CommitAsync();
        }
        var rowsBefore = await db.Transactions.CountAsync();
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.BankReceipts.CountAsync()).ShouldBe(1);
        var retained = await db.BankReceipts.AsNoTracking().SingleAsync();
        (retained.Id, retained.Amount, retained.Status).ShouldBe((receipt.Id, 90m, BankReceiptStatus.Pending));
        (await db.SettlementEvents.AsNoTracking().SingleAsync(e => e.BankReceiptId == receipt.Id)).Id.ShouldBe(deposit.Id);
        (await db.Transactions.CountAsync()).ShouldBe(rowsBefore);
        var unchanged = await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == legacy.Unpaid);
        (unchanged.Version, unchanged.Value, unchanged.ClassificationRevision, unchanged.PostedSettlementInstrument)
            .ShouldBe((original.Version, original.Value, original.ClassificationRevision, original.PostedSettlementInstrument));
        (await db.BankStatementMatches.CountAsync()).ShouldBe(0);
        (await db.CardSettlementBatches.CountAsync()).ShouldBe(0);
        (await db.CardSettlementFeeEvidence.CountAsync()).ShouldBe(0);
    }
}
