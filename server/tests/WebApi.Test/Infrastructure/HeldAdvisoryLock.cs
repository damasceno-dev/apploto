using Npgsql;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Holds one transaction-scoped PostgreSQL advisory lock on a side connection so a test can
/// queue concurrent requests behind it, observe them waiting, and release them in a known order.
/// </summary>
public sealed class HeldAdvisoryLock : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private readonly NpgsqlTransaction _transaction;
    private readonly long _advisoryKey;
    private bool _released;

    private HeldAdvisoryLock(NpgsqlConnection connection, NpgsqlTransaction transaction, long advisoryKey)
    {
        _connection = connection;
        _transaction = transaction;
        _advisoryKey = advisoryKey;
    }

    public static async Task<HeldAdvisoryLock> AcquireAsync(string connectionString, long advisoryKey)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction);
        command.Parameters.AddWithValue("key", advisoryKey);
        await command.ExecuteNonQueryAsync();
        return new HeldAdvisoryLock(connection, transaction, advisoryKey);
    }

    public async Task WaitForWaitersAsync(int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT COUNT(*)
                FROM pg_locks
                WHERE locktype = 'advisory'
                  AND database = (SELECT oid FROM pg_database WHERE datname = current_database())
                  AND classid = (((@key >> 32) & 4294967295)::oid)
                  AND objid = ((@key & 4294967295)::oid)
                  AND objsubid = 1
                  AND granted = false
                """,
                _connection,
                _transaction);
            command.Parameters.AddWithValue("key", _advisoryKey);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            if (count >= expectedCount)
                return;

            await Task.Delay(25);
        }

        throw new TimeoutException($"Expected {expectedCount} requests to wait on advisory key {_advisoryKey}.");
    }

    public async Task ReleaseAsync()
    {
        if (_released)
            return;

        _released = true;
        await _transaction.RollbackAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
        await _transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
