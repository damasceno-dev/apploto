using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class MonthLockCoordination(
    ServerDbContext dbContext,
    MonthLockCoordinationOptions options) : IMonthLockCoordination
{
    private const string PostgresLockNotAvailableSqlState = "55P03";

    public Task<IMonthLockCoordinationScope?> TryAcquireShared(Guid branchId, CancellationToken ct = default)
    {
        return TryAcquire(branchId, exclusive: false, ct);
    }

    public Task<IMonthLockCoordinationScope?> TryAcquireExclusive(Guid branchId, CancellationToken ct = default)
    {
        return TryAcquire(branchId, exclusive: true, ct);
    }

    private async Task<IMonthLockCoordinationScope?> TryAcquire(
        Guid branchId,
        bool exclusive,
        CancellationToken ct)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var lockTimeoutMilliseconds = checked((long)Math.Ceiling(options.LockTimeout.TotalMilliseconds));
            if (lockTimeoutMilliseconds <= 0)
                throw new InvalidOperationException("Month-lock coordination timeout must be positive.");

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('lock_timeout', {0}, true)",
                [$"{lockTimeoutMilliseconds}ms"],
                ct);

            var advisoryKey = MonthLockCoordinationKey.Compute(branchId);
            if (exclusive)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock({advisoryKey})",
                    ct);
            }
            else
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock_shared({advisoryKey})",
                    ct);
            }

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

    private sealed class CoordinationScope(IDbContextTransaction transaction) : IMonthLockCoordinationScope
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
