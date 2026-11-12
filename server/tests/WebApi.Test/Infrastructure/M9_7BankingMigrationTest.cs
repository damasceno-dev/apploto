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
    public async Task ResponsibilityUpgrade_PreservesLockedUnknownCustodyAndOfficialVariance_WithoutInventedAttribution()
    {
        await using var db = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031020206_M9_6Phase6To9FinancialFoundation");
        var legacy = await M9_7SettlementEventsMigrationTest.SeedPriorSchemaAsync(db);
        await migrator.MigrateAsync("20261105185138_M9_7AppendOnlyCardFeeEvidence");
        var terminal = new Terminal { BranchId = legacy.BranchId, Name = "Synthetic original terminal" };
        var account = new Account { BranchId = legacy.BranchId, Name = "Synthetic original cashbox", Type = AccountType.Terminal, TerminalId = terminal.Id };
        var product = new Product { BranchId = legacy.BranchId, Name = "Diferença Caixa" };
        var session = new DailyClose
        {
            BranchId = legacy.BranchId, AccountId = account.Id, OpenedByUserId = legacy.UserId,
            Date = new DateTime(2026, 9, 10), Status = DailyCloseStatus.Approved,
            MappingState = SessionMappingState.Unresolved, CustodyHistoryComplete = false, ReviewHistoryComplete = false
        };
        db.AddRange(terminal, account, product, session);
        db.DailyCloseItems.Add(new DailyCloseItem { DailyCloseId = session.Id, ProductId = product.Id, Value = -17m });
        await db.SaveChangesAsync();
        // A second, explicitly known synthetic source already has an attribution at the prior schema.
        // The upgrade must retain it; it may not assign the unknown legacy session above to this operator.
        var knownAccount = new Account { BranchId = legacy.BranchId, Name = "Known prior cashbox", Type = AccountType.Terminal, TerminalId = terminal.Id };
        var knownSession = new DailyClose
        {
            BranchId = legacy.BranchId, AccountId = knownAccount.Id, OpenedByUserId = legacy.UserId,
            Date = new DateTime(2026, 9, 11), Status = DailyCloseStatus.Approved, TerminalId = terminal.Id,
            CustodianOperatorId = legacy.OperatorId, MappingState = SessionMappingState.Ready, Sequence = 1,
            CustodyState = SessionCustodyState.Finished, StartedAt = new DateTime(2026, 9, 11, 11, 0, 0, DateTimeKind.Utc),
            EndedAt = new DateTime(2026, 9, 11, 20, 0, 0, DateTimeKind.Utc), ReviewHistoryComplete = false
        };
        var cash = new Product { BranchId = legacy.BranchId, Name = "Dinheiro" };
        var openingCount = new OpeningCount
        {
            BranchId = legacy.BranchId, AccountId = knownAccount.Id, AsOfDate = new DateTime(2026, 9, 10),
            Revision = 1, RecordedByUserId = legacy.UserId
        };
        openingCount.Items.Add(new OpeningCountItem { OpeningCountId = openingCount.Id, ProductId = cash.Id, Value = 1000m });
        var opening = new SessionOpening
        {
            BranchId = legacy.BranchId, SessionId = knownSession.Id, SourceKind = SessionOpeningSourceKind.OpeningCount,
            SourceId = openingCount.Id, SourceRevision = 1,
            EvidenceKind = SessionOpeningEvidenceKind.IncomingAcknowledgment, AcknowledgedByUserId = legacy.UserId,
            AcknowledgedByOperatorId = legacy.OperatorId, AcknowledgedAt = knownSession.StartedAt.Value
        };
        opening.Items.Add(new SessionOpeningItem
        {
            SessionOpeningId = opening.Id, ProductId = cash.Id, ProposedValue = 1000m, AcknowledgedValue = 980m
        });
        var discrepancy = new SessionOpeningDiscrepancy
        {
            BranchId = legacy.BranchId, SessionId = knownSession.Id, SessionOpeningId = opening.Id,
            SourceKind = SessionOpeningSourceKind.OpeningCount, CurrentRevision = 1, CurrentTotalDelta = -20m,
            Attribution = OpeningDiscrepancyAttribution.Incoming, AttributedRevision = 1
        };
        var attribution = new OpeningDiscrepancyAttributionEvent
        {
            DiscrepancyId = discrepancy.Id, DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Incoming,
            ActorUserId = legacy.UserId, Reason = "Known synthetic prior decision"
        };
        db.AddRange(knownAccount, knownSession, cash, openingCount, opening, discrepancy, attribution);
        db.SessionOpeningDiscrepancyRevisions.Add(new SessionOpeningDiscrepancyRevision
        {
            DiscrepancyId = discrepancy.Id, Revision = 1, SourceId = openingCount.Id, SourceRevision = 1,
            ProposedTotal = 1000m, AcknowledgedTotal = 980m, TotalDelta = -20m, CreatedByUserId = legacy.UserId,
            Items = [new SessionOpeningDiscrepancyRevisionItem { ProductId = cash.Id, ProposedValue = 1000m, AcknowledgedValue = 980m, Delta = -20m }]
        });
        await db.SaveChangesAsync();
        var oldDecision = await db.OpeningDiscrepancyAttributionEvents.AsNoTracking().SingleAsync(a => a.Id == attribution.Id);
        var oldSource = await db.SessionOpeningDiscrepancies.AsNoTracking().SingleAsync(d => d.Id == discrepancy.Id);
        var settingId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Settings" ("Id", "BranchId", "LockDate", "DailyTargetHours", "LunchDeductionOver6H", "LunchDeductionOver4H", "CreatedAt", "Active")
            VALUES ({settingId}, {legacy.BranchId}, DATE '2026-09-30', 7.33, 1, 0.25, now(), true)
            """);
        var before = await db.DailyCloses.AsNoTracking().SingleAsync(c => c.Id == session.Id);
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        var after = await db.DailyCloses.AsNoTracking().SingleAsync(c => c.Id == session.Id);
        (after.Version, after.Status, after.CustodianOperatorId, after.StartedAt, after.SubmittedAttemptId, after.CustodyHistoryComplete)
            .ShouldBe((before.Version, before.Status, before.CustodianOperatorId, before.StartedAt, before.SubmittedAttemptId, false));
        (await db.OpeningDiscrepancyAdjustments.CountAsync()).ShouldBe(0);
        (await db.Settings.AsNoTracking().SingleAsync()).PersonalShortageThreshold.ShouldBeNull();
        var result = await new server.Infrastructure.Repositories.FinancialResultRepository(db).Read(legacy.BranchId, product.Id,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), null, 1, 50);
        result.Totals.Single(t => t.FinancialClass == FinancialClass.CashDifference).KnownSignedTotal.ShouldBe(-17m);
        result.Entries.Single(e => e.SessionId == session.Id).SourceHistoryComplete.ShouldBeFalse();
        var personal = await new server.Infrastructure.Repositories.PersonalDifferencesRepository(db).Read(legacy.BranchId, product.Id,
            new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), null, 1, 50);
        var unknown = personal.Entries.Single(e => e.SourceId == session.Id);
        unknown.OperatorId.ShouldBeNull(); unknown.SourceSignedAmount.ShouldBe(-17m);
        personal.Totals.Single(t => t.OperatorId == null).Shortage.ShouldBe(0m);
        personal.Totals.Single(t => t.OperatorId == legacy.OperatorId).Shortage.ShouldBe(20m);
        var retainedDecision = await db.OpeningDiscrepancyAttributionEvents.AsNoTracking().SingleAsync(a => a.Id == attribution.Id);
        (retainedDecision.DiscrepancyId, retainedDecision.DiscrepancyRevision, retainedDecision.Target, retainedDecision.ActorUserId,
            retainedDecision.Reason, retainedDecision.CreatedAt, retainedDecision.SupersedesEventId)
            .ShouldBe((oldDecision.DiscrepancyId, oldDecision.DiscrepancyRevision, oldDecision.Target, oldDecision.ActorUserId,
                oldDecision.Reason, oldDecision.CreatedAt, oldDecision.SupersedesEventId));
        (await db.SessionOpeningDiscrepancies.AsNoTracking().SingleAsync(d => d.Id == discrepancy.Id)).Version.ShouldBe(oldSource.Version);
    }

    [Fact]
    public async Task UpgradeRetainsExistingPendingChequeReceiptChainAndFrozenOriginalWithoutNewMoney()
    {
        await using var db = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031020206_M9_6Phase6To9FinancialFoundation");
        var legacy = await M9_7SettlementEventsMigrationTest.SeedPriorSchemaAsync(db);
        await migrator.MigrateAsync("20261103130725_M9_7Phase1To2SettlementEvents");
        var original = await db.Transactions.AsNoTracking().Where(t => t.Id == legacy.Unpaid).Select(t=>new {t.Version,t.Value,t.ClassificationRevision,t.PostedSettlementInstrument}).SingleAsync();
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
