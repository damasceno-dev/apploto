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

public sealed class M9_8EmployeeRecordsMigrationTest : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(p => { foreach (var bindings in p.HostConfig!.PortBindings!.Values) foreach (var binding in bindings) binding.HostIP = "127.0.0.1"; })
        .WithDatabase("loto_m98_upgrade").WithUsername("postgres").WithPassword("postgres").Build();
    public Task InitializeAsync() => postgres.StartAsync();
    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();
    [Fact]
    public async Task PriorSchemaUpgrade_PreservesInactiveIdentityAttendanceOfficialHistoryFinanceAndLocks_WithoutInventedPeopleBalances()
    {
        await using var db = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031020206_M9_6Phase6To9FinancialFoundation");
        var legacy = await M9_7SettlementEventsMigrationTest.SeedPriorSchemaAsync(db);
        await migrator.MigrateAsync("20261105202250_M9_7PersonalResponsibility");
        var setting = new Setting { BranchId = legacy.BranchId, LockDate = new DateTime(2026, 9, 30), DailyTargetHours = 8, LunchDeductionOver6H = 1 };
        var policy = new TimeEntryPolicy { BranchId = legacy.BranchId, EffectiveFrom = DateTime.MinValue, DailyTargetHours = 8, LunchDeductionOver6H = 1 };
        var attendance = new TimeEntry { BranchId = legacy.BranchId, OperatorId = legacy.OperatorId, Date = new DateTime(2026, 9, 10), Status = TimeEntryStatus.DayOff, TotalHours = 0, BalanceHours = -8 };
        db.AddRange(setting, policy, attendance); await db.SaveChangesAsync();
        var op = await db.Operators.SingleAsync(o => o.Id == legacy.OperatorId); op.Active = false; await db.SaveChangesAsync();
        var rowsBefore = await db.Transactions.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Id, r.Value, r.Version, r.ClassificationRevision, r.PostedSettlementInstrument }).ToListAsync();
        var personalBefore = await db.OpeningDiscrepancyAdjustments.CountAsync();
        db.ChangeTracker.Clear(); await db.Database.MigrateAsync();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Operators.AsNoTracking().SingleAsync(o => o.Id == legacy.OperatorId)).Active.ShouldBeFalse();
        (await db.TimeEntries.AsNoTracking().SingleAsync(t => t.Id == attendance.Id)).BalanceHours.ShouldBe(-8);
        (await db.Settings.AsNoTracking().SingleAsync(t => t.Id == setting.Id)).LockDate.ShouldBe(new DateTime(2026, 9, 30));
        (await db.Transactions.AsNoTracking().OrderBy(r => r.Id).Select(r => new { r.Id, r.Value, r.Version, r.ClassificationRevision, r.PostedSettlementInstrument }).ToListAsync()).ShouldBe(rowsBefore);
        (await db.OpeningDiscrepancyAdjustments.CountAsync()).ShouldBe(personalBefore);
        (await db.EmployeeCommissionFeePostings.CountAsync()).ShouldBe(0);
        (await db.EmployeeCommissionRules.CountAsync()).ShouldBe(0); (await db.EmployeePayItems.CountAsync()).ShouldBe(0);
        (await db.HourBankEntries.CountAsync()).ShouldBe(0); (await db.SpecialDraws.CountAsync()).ShouldBe(0);
        (await db.SpecialDrawAllocations.CountAsync()).ShouldBe(0); (await db.EmployeeCommissionSources.CountAsync()).ShouldBe(0);
        (await db.BranchUsers.CountAsync()).ShouldBe(0); // employee row never becomes a login/membership
    }
    [Fact]
    public async Task ClassificationRevisionUpgrade_PreservesEarlierPublications_AsUnknownVersion_WithoutBackfill()
    {
        await using var db = new ServerDbContext(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261031020206_M9_6Phase6To9FinancialFoundation");
        var legacy = await M9_7SettlementEventsMigrationTest.SeedPriorSchemaAsync(db);
        await migrator.MigrateAsync("20261106202826_M9_8CommissionFeePostings");
        var day = new DateTime(2026, 9, 10);
        var terminal = new Terminal { BranchId = legacy.BranchId, Name = "Synthetic historical terminal" };
        var account = new Account { BranchId = legacy.BranchId, Name = "Synthetic historical cashbox", Type = AccountType.Terminal, TerminalId = terminal.Id };
        var category = new Category { BranchId = legacy.BranchId, Name = "Explicit fee", DefaultDirection = Direction.In };
        var type = new TransactionType { CategoryId = category.Id, Name = "Historical fee", FinancialClass = FinancialClass.ShopFeesAndIncome, MonthlyFinancialRole = MonthlyFinancialRole.BolaoServiceFee };
        var session = new DailyClose
        {
            BranchId = legacy.BranchId,
            AccountId = account.Id,
            OpenedByUserId = legacy.UserId,
            Date = day,
            Status = DailyCloseStatus.Submitted,
            CustodianOperatorId = legacy.OperatorId,
            TerminalId = terminal.Id,
            MappingState = SessionMappingState.Ready,
            CustodyHistoryComplete = true,
            Sequence = 1,
            CustodyState = SessionCustodyState.Finished,
            StartedAt = DateTime.SpecifyKind(day.AddHours(12), DateTimeKind.Utc),
            EndedAt = DateTime.SpecifyKind(day.AddHours(13), DateTimeKind.Utc)
        };
        var attempt = new SessionCountAttempt
        {
            BranchId = legacy.BranchId,
            SessionId = session.Id,
            AttemptNumber = 1,
            ConfirmationToken = Guid.NewGuid(),
            ConferredByUserId = legacy.UserId,
            ConferredByName = "Historical synthetic count",
            CommandKey = Guid.NewGuid().ToString("N")
        };
        var transaction = new Transaction
        {
            BranchId = legacy.BranchId,
            AccountId = account.Id,
            CategoryId = category.Id,
            TransactionTypeId = type.Id,
            SessionId = session.Id,
            RecordedByOperatorId = legacy.OperatorId,
            CreatedByUserId = legacy.UserId,
            Date = day,
            DueDate = day,
            Value = 100,
            Direction = Direction.In
        };
        // Owned migration fixture: establish an explicit, internally linked prior-schema snapshot.
        await using (var setup = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL session_replication_role = replica");
            db.AddRange(terminal, account, category, type, session, new Setting { BranchId = legacy.BranchId });
            await db.SaveChangesAsync();
            db.AddRange(attempt, transaction); await db.SaveChangesAsync();
            session.SubmittedAttemptId = attempt.Id; await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Transactions\" SET \"PostedFinancialClass\"=2, \"PostedMonthlyFinancialRole\"=1, \"ClassificationRevision\"=1, \"ClassificationProvenance\"=1 WHERE \"Id\"={transaction.Id}");
            await setup.CommitAsync();
        }
        var publication = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "EmployeeCommissionFeePostings" ("Id", "BranchId", "OperatorId", "SessionId", "AttemptId", "TransactionId", "SourceDate", "PostedOn", "Component", "SignedAmount", "ClassificationProvenance", "CreatedAt", "Active")
            VALUES ({publication}, {legacy.BranchId}, {legacy.OperatorId}, {session.Id}, {attempt.Id}, {transaction.Id}, {DateOnly.FromDateTime(day)}, {DateOnly.FromDateTime(day)}, 1, 100, 1, now(), true)
            """);
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        var retained = await db.EmployeeCommissionFeePostings.AsNoTracking().SingleAsync(p => p.Id == publication);
        retained.ClassificationRevision.ShouldBeNull(); retained.SignedAmount.ShouldBe(100);
        retained.SourceDate.ShouldBe(day); retained.PostedOn.ShouldBe(day); retained.AttemptId.ShouldBe(attempt.Id); retained.TransactionId.ShouldBe(transaction.Id);
        await migrator.MigrateAsync("20261106202826_M9_8CommissionFeePostings");
        await db.Database.MigrateAsync();
        (await db.EmployeeCommissionFeePostings.AsNoTracking().SingleAsync(p => p.Id == publication)).ClassificationRevision.ShouldBeNull();
        (await db.Transactions.AsNoTracking().SingleAsync(t => t.Id == transaction.Id)).Value.ShouldBe(100);
    }
}
