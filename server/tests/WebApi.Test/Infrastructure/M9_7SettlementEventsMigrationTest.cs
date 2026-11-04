using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M9.7 Phases 1.3–1.4 upgrade on real PostgreSQL from the prior (M9.6) schema: each legacy <c>PaidAt</c> becomes one
/// reconstructed event with <c>OccurredOn = PostedOn</c> = its São Paulo date and the original instant kept; nothing
/// is a new payment (no receipt, no row); anomalies are flagged, not corrected; posted rows get the type's markers as
/// migrated provenance; the downgrade restores <c>PaidAt</c> only while no post-upgrade history exists.
/// </summary>
public sealed class M9_7SettlementEventsMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261031020206_M9_6Phase6To9FinancialFoundation";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m9_7_settlement").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task Upgrade_ShouldReconstructEachPaidAtOnce_WithoutNewMoney_AndDowngradeOnlyWhileNothingWasPostedSince()
    {
        await using var db = Context();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var legacy = await SeedPriorSchemaAsync(db);
        var paidBefore = await Strings("""SELECT "Id"::text || '|' || "PaidAt"::text FROM "Transactions" WHERE "PaidAt" IS NOT NULL ORDER BY "Id" """);

        await db.Database.MigrateAsync();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await Strings("""SELECT column_name::text FROM information_schema.columns WHERE table_name = 'Transactions' AND column_name = 'PaidAt'"""))
            .ShouldBeEmpty();
        (await Strings("""SELECT count(*)::text FROM "Transactions" """)).ShouldBe(["7"]);
        (await Strings("""SELECT count(*)::text FROM "BankReceipts" """)).ShouldBe(["0"]);

        var events = await db.SettlementEvents.AsNoTracking().ToListAsync();
        events.Count.ShouldBe(6);
        events.ShouldAllBe(e => e.Kind == SettlementEventKind.MigratedSettlement && e.Provenance == SettlementEventProvenance.MigratedFromPaidAt
                                && e.Sequence == 1 && e.CreatedByUserId == null && e.OccurredOn == e.PostedOn);
        Event(events, legacy.BankPaid).ShouldBe((new DateTime(2026, 9, 15), -120m, SettlementMigrationIssue.None));
        // 01:30 UTC on 16/09 is 22:30 on 15/09 in São Paulo: the branch-local business date.
        Event(events, legacy.BankLateNight).ShouldBe((new DateTime(2026, 9, 15), -80m, SettlementMigrationIssue.None));
        Event(events, legacy.TabPaid).ShouldBe((new DateTime(2026, 9, 12), 0m, SettlementMigrationIssue.NotSettleableTabRow));
        Event(events, legacy.DraftPaid).ShouldBe((new DateTime(2026, 9, 14), -30m, SettlementMigrationIssue.DraftAtMigration));
        Event(events, legacy.CancelledPaid).ShouldBe((new DateTime(2026, 9, 14), -40m, SettlementMigrationIssue.CancelledAtMigration));
        Event(events, legacy.BeforeDate).ShouldBe((new DateTime(2026, 9, 18), -60m, SettlementMigrationIssue.OccurredBeforeTransactionDate));
        events.Single(e => e.ReceivableTransactionId == legacy.BankPaid).SourcePaidAt
            .ShouldBe(new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc));
        events.ShouldNotContain(e => e.ReceivableTransactionId == legacy.Unpaid);

        // Posted rows carry the type's markers with migrated provenance; never-posted rows carry none.
        var rows = await db.Transactions.AsNoTracking().ToDictionaryAsync(t => t.Id);
        (rows[legacy.BankPaid].PostedSettlementInstrument, rows[legacy.BankPaid].ClassificationProvenance, rows[legacy.BankPaid].ClassificationRevision)
            .ShouldBe(((SettlementInstrument?)SettlementInstrument.Cheque, (ClassificationProvenance?)ClassificationProvenance.MigratedFromTypeAtUpgrade, 1));
        rows[legacy.Unpaid].ClassificationProvenance.ShouldBe(ClassificationProvenance.MigratedFromTypeAtUpgrade);
        rows[legacy.DraftPaid].ClassificationRevision.ShouldBe(0);
        rows[legacy.CancelledPaid].ClassificationRevision.ShouldBe(0);
        (await Strings($"""SELECT "SystemRole"::text || ':' || "FinancialClass"::text FROM "TransactionTypes" WHERE "SystemRole" IS NOT NULL ORDER BY 1"""))
            .ShouldBe(["1:1", "9:1"]);

        // One reconstructed event per receivable, whatever re-runs. The Tab row's event settles nothing, so only the
        // unique index (not the outstanding-amount guard) stands between it and a second reconstruction.
        var duplicate = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SettlementEvents" ("Id", "BranchId", "ReceivableTransactionId", "Sequence", "Kind", "Amount",
                "OutstandingDelta", "OccurredOn", "PostedOn", "Provenance", "MigrationIssue", "SourcePaidAt",
                "SettlementInstrument", "CreatedAt", "Active")
            VALUES ({Guid.NewGuid()}, {legacy.BranchId}, {legacy.TabPaid}, 1, 1, 50, 0, DATE '2026-09-12', DATE '2026-09-12',
                2, 1, now(), 1, now(), true)
            """));
        duplicate.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        // A second settling reconstruction of a cheque never gets that far: it would settle more than the face value.
        var overSettle = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SettlementEvents" ("Id", "BranchId", "ReceivableTransactionId", "Sequence", "Kind", "Amount",
                "OutstandingDelta", "OccurredOn", "PostedOn", "Provenance", "MigrationIssue", "SourcePaidAt",
                "SettlementInstrument", "CreatedAt", "Active")
            VALUES ({Guid.NewGuid()}, {legacy.BranchId}, {legacy.BankPaid}, 1, 1, 120, -120, DATE '2026-09-15', DATE '2026-09-15',
                2, 0, now(), 1, now(), true)
            """));
        overSettle.ConstraintName.ShouldBe("CK_SettlementEvents_OutstandingWithinValue");

        // Downgrade before any post-upgrade history: PaidAt comes back exactly; re-upgrading reconstructs the same events.
        await migrator.MigrateAsync(Previous);
        (await Strings("""SELECT "Id"::text || '|' || "PaidAt"::text FROM "Transactions" WHERE "PaidAt" IS NOT NULL ORDER BY "Id" """))
            .ShouldBe(paidBefore);
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        (await db.SettlementEvents.AsNoTracking().CountAsync()).ShouldBe(6);

        // A row posted after the upgrade is history the downgrade would erase: it refuses and changes nothing.
        await InsertTransactionAsync(db, legacy, legacy.BankAccountId, Direction.Out, 1, new DateTime(2026, 10, 1), 10m, paidAt: null, withPaidAt: false);
        var refused = await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(Previous));
        refused.MessageText.ShouldContain("M97_DOWNGRADE");
        (await db.SettlementEvents.AsNoTracking().CountAsync()).ShouldBe(6);
        (await Strings("""SELECT column_name::text FROM information_schema.columns WHERE table_name = 'Transactions' AND column_name = 'PaidAt'"""))
            .ShouldBeEmpty();
    }

    private static (DateTime OccurredOn, decimal Delta, SettlementMigrationIssue Issue) Event(IEnumerable<SettlementEvent> events, Guid id)
    {
        var e = events.Single(e => e.ReceivableTransactionId == id);
        return (e.OccurredOn, e.OutstandingDelta, e.MigrationIssue);
    }

    private sealed record Legacy(
        Guid BranchId, Guid UserId, Guid OperatorId, Guid BankAccountId, Guid TabAccountId, Guid ClientId,
        Guid ChequeTypeId, Guid OutCategoryId,
        Guid BankPaid, Guid BankLateNight, Guid TabPaid, Guid DraftPaid, Guid CancelledPaid, Guid BeforeDate, Guid Unpaid);

    private static async Task<Legacy> SeedPriorSchemaAsync(ServerDbContext db)
    {
        var branch = new Branch { Name = "Legacy cheques" };
        var user = new User { Name = "Gerente", Email = $"legacy-{Guid.NewGuid():N}@example.test", Password = "x" };
        var op = new Operator { Name = "Operador", BranchId = branch.Id };
        var bank = new Account { Type = AccountType.BankAccount, Name = "Banco", BranchId = branch.Id };
        var tab = new Account { Type = AccountType.Tab, Name = "Fiado", BranchId = branch.Id };
        var client = new Client { Name = "Cliente", Phone = "11999999999", BranchId = branch.Id };
        var saidas = new Category { BranchId = branch.Id, Name = "Saídas", DefaultDirection = Direction.Out };
        var system = new Category { BranchId = branch.Id, Name = "Movimentos automáticos de fiado (saída)", DefaultDirection = Direction.Out };
        db.AddRange(branch, user, op, bank, tab, client, saidas, system);
        await db.SaveChangesAsync();

        var chequeType = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "CreatedAt",
                "Active", "SettlementInstrument", "MonthlyFinancialRole")
            VALUES ({chequeType}, 'Depósito Cheque', 4, false, {saidas.Id}, now(), true, 1, 0);
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "CreatedAt",
                "Active", "SettlementInstrument", "MonthlyFinancialRole", "SystemRole")
            VALUES ({Guid.NewGuid()}, 'Saída de caixa — venda fiado', 0, false, {system.Id}, now(), true, 0, 0, 1);
            """);

        var legacy = new Legacy(branch.Id, user.Id, op.Id, bank.Id, tab.Id, client.Id, chequeType, saidas.Id,
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        return legacy with
        {
            BankPaid = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 1, new DateTime(2026, 9, 10), 120m,
                new DateTime(2026, 9, 15, 15, 0, 0, DateTimeKind.Utc)),
            BankLateNight = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 1, new DateTime(2026, 9, 10), 80m,
                new DateTime(2026, 9, 16, 1, 30, 0, DateTimeKind.Utc)),
            TabPaid = await InsertTransactionAsync(db, legacy, tab.Id, Direction.Out, 1, new DateTime(2026, 9, 10), 50m,
                new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc), clientId: client.Id),
            DraftPaid = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 0, new DateTime(2026, 9, 10), 30m,
                new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc)),
            CancelledPaid = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 2, new DateTime(2026, 9, 10), 40m,
                new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc)),
            BeforeDate = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 1, new DateTime(2026, 9, 20), 60m,
                new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc)),
            Unpaid = await InsertTransactionAsync(db, legacy, bank.Id, Direction.Out, 1, new DateTime(2026, 9, 10), 90m, null)
        };
    }

    private static async Task<Guid> InsertTransactionAsync(
        ServerDbContext db, Legacy legacy, Guid accountId, Direction direction, short status, DateTime date, decimal value,
        DateTime? paidAt, Guid? clientId = null, bool withPaidAt = true)
    {
        var id = Guid.NewGuid();
        if (withPaidAt)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Transactions" ("Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId",
                    "ClientId", "DueDate", "PaidAt", "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "CreatedAt", "Active")
                VALUES ({id}, {DateOnly.FromDateTime(date)}, {value}, {legacy.ChequeTypeId}, {legacy.OutCategoryId}, {(short)direction}, {accountId},
                    {clientId}, {DateOnly.FromDateTime(date)}, {paidAt}, {legacy.OperatorId}, {legacy.UserId}, {status}, {legacy.BranchId}, now(), true)
                """);
        }
        else
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Transactions" ("Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId",
                    "ClientId", "DueDate", "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "CreatedAt", "Active")
                VALUES ({id}, {DateOnly.FromDateTime(date)}, {value}, {legacy.ChequeTypeId}, {legacy.OutCategoryId}, {(short)direction}, {accountId},
                    {clientId}, {DateOnly.FromDateTime(date)}, {legacy.OperatorId}, {legacy.UserId}, {status}, {legacy.BranchId}, now(), true)
                """);
        }
        return id;
    }

    private async Task<List<string>> Strings(string sql)
    {
        await using var connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
            values.Add(reader.GetString(0));
        return values;
    }
}
