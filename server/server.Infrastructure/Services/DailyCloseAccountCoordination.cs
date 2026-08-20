using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class DailyCloseAccountCoordination(
    ServerDbContext dbContext,
    DailyCloseAccountCoordinationOptions options,
    ILogger<DailyCloseAccountCoordination> logger) : IDailyCloseAccountCoordination
{
    private static readonly EventId AcquiredEvent = new(7701, "DailyCloseAccountCoordinationAcquired");
    private static readonly EventId ReleasedEvent = new(7702, "DailyCloseAccountCoordinationReleased");

    public async Task<IDailyCloseAccountCoordinationScope> Acquire(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var lockTimeoutMilliseconds = checked((long)Math.Ceiling(options.LockTimeout.TotalMilliseconds));
            if (lockTimeoutMilliseconds <= 0)
                throw new InvalidOperationException("DailyClose account coordination lock timeout must be positive.");

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('lock_timeout', {0}, true)",
                [$"{lockTimeoutMilliseconds}ms"],
                ct);

            // Canonical order for every close/ledger mutation: the shared branch month-lock
            // boundary is always acquired before the Phase 3 account-history key. The
            // lock-month command takes only the exclusive branch key, so no path can invert
            // this order and form a deadlock cycle.
            var waitStartedAt = Stopwatch.GetTimestamp();
            var monthLockKey = MonthLockCoordinationKey.Compute(branchId);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock_shared({monthLockKey})",
                ct);

            var advisoryKey = DailyCloseAccountCoordinationKey.Compute(branchId, accountId);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({advisoryKey})",
                ct);
            var acquiredAt = Stopwatch.GetTimestamp();

            logger.LogInformation(
                AcquiredEvent,
                "Daily-close account coordination acquired for branch {BranchId} and account {AccountId} " +
                "after {WaitDurationMilliseconds} ms (timeout {LockTimeoutMilliseconds} ms).",
                branchId,
                accountId,
                Stopwatch.GetElapsedTime(waitStartedAt, acquiredAt).TotalMilliseconds,
                lockTimeoutMilliseconds);

            return new CoordinationScope(transaction, logger, branchId, accountId, acquiredAt);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class CoordinationScope(
        IDbContextTransaction transaction,
        ILogger logger,
        Guid branchId,
        Guid accountId,
        long acquiredAt) : IDailyCloseAccountCoordinationScope
    {
        private bool _completed;
        private bool _releaseMeasured;

        public async Task Complete(CancellationToken ct = default)
        {
            if (_completed)
                return;

            await transaction.CommitAsync(ct);
            _completed = true;
            MeasureRelease(completed: true);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            MeasureRelease(_completed);
        }

        private void MeasureRelease(bool completed)
        {
            if (_releaseMeasured)
                return;

            _releaseMeasured = true;
            logger.LogInformation(
                ReleasedEvent,
                "Daily-close account coordination released for branch {BranchId} and account {AccountId} " +
                "after holding the boundary for {HoldDurationMilliseconds} ms; committed: {Committed}.",
                branchId,
                accountId,
                Stopwatch.GetElapsedTime(acquiredAt).TotalMilliseconds,
                completed);
        }
    }
}
