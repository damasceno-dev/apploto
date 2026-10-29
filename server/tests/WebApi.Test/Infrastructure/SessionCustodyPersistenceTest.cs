using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// The M9.6 Phase 2 database guards hold for writers that bypass the application: non-overlapping Ready custody
/// intervals (including competing writers on independent connections and an initially empty history), retained Ready
/// history, cashbox rows bound to a session of the same cashbox, and row/session business-date coherence.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionCustodyPersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task IntervalGuard_ShouldRejectAnOverlappingReadySession_AndAllowAnExactBoundary()
    {
        var (branch, account, custodian) = await SeedCashboxAsync("IntervalOverlap");
        var day = new DateTime(2026, 6, 10);
        await InsertSessionAsync(branch, account, custodian, 1, day, At(day, 9), At(day, 11));

        var overlap = await Should.ThrowAsync<PostgresException>(() =>
            InsertSessionAsync(branch, account, custodian, 2, day, At(day, 10), At(day, 12)));
        overlap.ConstraintName.ShouldBe("CK_DailyCloses_SessionIntervalOverlap");

        await Should.NotThrowAsync(() => InsertSessionAsync(branch, account, custodian, 2, day, At(day, 11), At(day, 12)));
        var unboundedOverlap = await Should.ThrowAsync<PostgresException>(() =>
            InsertSessionAsync(branch, account, custodian, 3, day, At(day, 8), null));
        unboundedOverlap.ConstraintName.ShouldBe("CK_DailyCloses_SessionIntervalOverlap");
    }

    [Fact]
    public async Task IntervalGuard_ShouldSerializeCompetingWriters_OnAnInitiallyEmptyHistory()
    {
        var (branch, account, custodian) = await SeedCashboxAsync("IntervalRace");
        var day = new DateTime(2026, 6, 11);
        await using var first = await OpenAsync();
        await using var second = await OpenAsync();
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync();

        await InsertSessionAsync(first, branch, account, custodian, 1, day, At(day, 9), null);
        var competing = InsertSessionAsync(second, branch, account, custodian, 2, day, At(day, 10), null);
        await Task.Delay(300);
        competing.IsCompleted.ShouldBeFalse();
        await firstTransaction.CommitAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => competing);
        exception.ConstraintName.ShouldBe("CK_DailyCloses_SessionIntervalOverlap");
    }

    [Theory]
    [InlineData(System.Data.IsolationLevel.RepeatableRead)]
    [InlineData(System.Data.IsolationLevel.Serializable)]
    public async Task IntervalGuard_ShouldAbortAWriterWhoseSnapshotPredatesTheCompetingCommit(System.Data.IsolationLevel isolation)
    {
        var (branch, account, custodian) = await SeedCashboxAsync($"IntervalSnapshot{isolation}");
        var day = new DateTime(2026, 6, 15);
        await using var first = await OpenAsync();
        await using var second = await OpenAsync();
        await using var firstTransaction = await first.BeginTransactionAsync();
        await using var secondTransaction = await second.BeginTransactionAsync(isolation);

        // The second writer takes its snapshot before the first commits, so it can never check a stale history.
        await using (var snapshot = new NpgsqlCommand("SELECT count(*) FROM \"DailyCloses\"", second))
            await snapshot.ExecuteScalarAsync();
        await InsertSessionAsync(first, branch, account, custodian, 1, day, At(day, 9), null);
        var competing = InsertSessionAsync(second, branch, account, custodian, 2, day, At(day, 14), At(day, 15));
        await Task.Delay(300);
        competing.IsCompleted.ShouldBeFalse();
        await firstTransaction.CommitAsync();

        var exception = await Should.ThrowAsync<PostgresException>(() => competing);
        exception.SqlState.ShouldBe(PostgresErrorCodes.SerializationFailure);
    }

    [Fact]
    public async Task HistoryGuard_ShouldRejectDeletingDeactivatingOrDowngradingReadyHistory()
    {
        var (branch, account, custodian) = await SeedCashboxAsync("HistoryGuard");
        var day = new DateTime(2026, 6, 12);
        var id = await InsertSessionAsync(branch, account, custodian, 1, day, At(day, 9), At(day, 10));

        foreach (var sql in new[]
                 {
                     $"DELETE FROM \"DailyCloses\" WHERE \"Id\" = '{id}'",
                     $"UPDATE \"DailyCloses\" SET \"Active\" = false WHERE \"Id\" = '{id}'",
                     $"UPDATE \"DailyCloses\" SET \"MappingState\" = 1 WHERE \"Id\" = '{id}'"
                 })
        {
            var exception = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync(sql));
            exception.ConstraintName.ShouldBe("CK_DailyCloses_ReadyHistoryRetained");
        }
    }

    [Fact]
    public async Task RowGuards_ShouldRequireASessionOfTheSameCashboxAndItsBusinessDate()
    {
        var (branch, account, custodian) = await SeedCashboxAsync("RowGuard");
        var otherCashbox = await factory.SeedAccountAsync(branch, AccountType.Terminal);
        var day = new DateTime(2026, 6, 13);
        var sessionId = await InsertSessionAsync(branch, account, custodian, 1, day, At(day, 9), At(day, 10));
        var category = await factory.SeedCategoryAsync(branch, defaultDirection: Direction.In);
        var type = await factory.SeedTransactionTypeAsync(category.Id);
        var user = await factory.SeedUserAsync();

        async Task<PostgresException> InsertRow(Guid accountId, Guid? rowSessionId, DateTime date) =>
            await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync($"""
                INSERT INTO "Transactions" ("Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId",
                    "DueDate", "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "SessionId", "CreatedAt", "Active")
                VALUES ('{Guid.NewGuid()}', '{date:yyyy-MM-dd}', 10, '{type.Id}', '{category.Id}', 0, '{accountId}',
                    '{date:yyyy-MM-dd}', '{custodian}', '{user.Id}', 0, '{branch}',
                    {(rowSessionId is null ? "NULL" : $"'{rowSessionId}'")}, now(), true)
                """));

        (await InsertRow(account, null, day)).ConstraintName.ShouldBe("CK_Transactions_CashboxRowRequiresSession");
        (await InsertRow(otherCashbox.Id, sessionId, day)).ConstraintName
            .ShouldBe("FK_Transactions_DailyCloses_BranchId_AccountId_SessionId");
        (await InsertRow(account, sessionId, day.AddDays(1))).ConstraintName.ShouldBe("CK_Transactions_SessionDateCoherence");
    }

    [Fact]
    public async Task ActiveCustodyIndex_ShouldAllowOnlyOneActiveSessionPerCashbox()
    {
        var (branch, account, custodian) = await SeedCashboxAsync("ActiveIndex");
        var day = new DateTime(2026, 6, 14);
        await InsertSessionAsync(branch, account, custodian, 1, day, At(day, 9), null);

        var exception = await Should.ThrowAsync<PostgresException>(() =>
            InsertSessionAsync(branch, account, custodian, 2, day.AddDays(1), At(day.AddDays(1), 9), null));

        exception.ConstraintName.ShouldBeOneOf("IX_DailyCloses_BranchId_AccountId_ActiveCustody", "CK_DailyCloses_SessionIntervalOverlap");
    }

    private async Task<(Guid Branch, Guid Account, Guid Custodian)> SeedCashboxAsync(string label)
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var custodian = await factory.SeedOperatorAsync(branch.Id);
        return (branch.Id, account.Id, custodian.Id);
    }

    private static DateTime At(DateTime day, int saoPauloHour) =>
        DateTime.SpecifyKind(day.Date.AddHours(saoPauloHour + 3), DateTimeKind.Utc);

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(factory.DisposableConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task<Guid> InsertSessionAsync(
        Guid branch, Guid account, Guid custodian, int sequence, DateTime day, DateTime startedAt, DateTime? endedAt)
    {
        await using var connection = await OpenAsync();
        return await InsertSessionAsync(connection, branch, account, custodian, sequence, day, startedAt, endedAt);
    }

    private async Task<Guid> InsertSessionAsync(
        NpgsqlConnection connection, Guid branch, Guid account, Guid custodian, int sequence, DateTime day,
        DateTime startedAt, DateTime? endedAt)
    {
        var id = Guid.NewGuid();
        var opener = await factory.SeedUserAsync();
        await using var command = new NpgsqlCommand("""
            INSERT INTO "DailyCloses" ("Id", "Date", "Status", "AccountId", "OpenedByUserId", "BranchId", "ReviewHistoryComplete",
                "CreatedAt", "Active", "MappingState", "Origin", "CustodyState", "CustodianOperatorId", "Sequence", "StartedAt",
                "EndedAt", "LedgerRevision", "CountRevision", "CustodyHistoryComplete")
            VALUES (@id, @date, 0, @account, @opener, @branch, true, now(), true, 0, 0, @state, @custodian, @sequence, @started,
                @ended, 0, 0, true)
            """, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("date", day.Date);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("opener", opener.Id);
        command.Parameters.AddWithValue("branch", branch);
        command.Parameters.AddWithValue("state", (short)(endedAt is null ? SessionCustodyState.Active : SessionCustodyState.Finished));
        command.Parameters.AddWithValue("custodian", custodian);
        command.Parameters.AddWithValue("sequence", sequence);
        command.Parameters.AddWithValue("started", startedAt);
        command.Parameters.AddWithValue("ended", (object?)endedAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
        return id;
    }
}
