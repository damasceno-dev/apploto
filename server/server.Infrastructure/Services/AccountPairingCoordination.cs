using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class AccountPairingCoordination(
    ServerDbContext dbContext,
    AccountPairingCoordinationOptions options) : IAccountPairingCoordination
{
    private const string PostgresLockNotAvailableSqlState = "55P03";

    public async Task<IAccountPairingCoordinationScope?> TryAcquire(Guid branchId, CancellationToken ct = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var lockTimeoutMilliseconds = checked((long)Math.Ceiling(options.LockTimeout.TotalMilliseconds));
            if (lockTimeoutMilliseconds <= 0)
                throw new InvalidOperationException("Account pairing coordination timeout must be positive.");

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('lock_timeout', {0}, true)",
                [$"{lockTimeoutMilliseconds}ms"],
                ct);

            var advisoryKey = AccountPairingCoordinationKey.Compute(branchId);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({advisoryKey})",
                ct);

            return new CoordinationScope(transaction);
        }
        catch (Exception exception) when (IsLockTimeout(exception))
        {
            await transaction.DisposeAsync();
            return null;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private static bool IsLockTimeout(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresLockNotAvailableSqlState })
                return true;
        }

        return false;
    }

    private sealed class CoordinationScope(IDbContextTransaction transaction) : IAccountPairingCoordinationScope
    {
        private bool _completed;

        public async Task Complete(CancellationToken ct = default)
        {
            if (_completed)
                return;

            await transaction.CommitAsync(ct);
            _completed = true;
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }
    }
}
