using Microsoft.EntityFrameworkCore;

namespace server.Infrastructure.Services;

/// <summary>
/// Deletes login-throttle rows past <c>ExpiresAt</c> and recovery-throttle rows whose cooldown has elapsed, in bounded
/// batches. Such a row means exactly what a missing row means, and <c>SKIP LOCKED</c> leaves any row a request is
/// holding <c>FOR UPDATE</c> to that request.
/// </summary>
internal sealed class SecurityThrottleCleanup(ServerDbContext dbContext)
{
    public async Task<SecurityThrottleCleanupResult> DeleteExpiredBatch(DateTime utcNow, int batchSize, CancellationToken ct = default)
    {
        var login = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "LoginThrottles" WHERE "Id" IN (
                SELECT "Id" FROM "LoginThrottles" WHERE "ExpiresAt" <= {utcNow}
                ORDER BY "ExpiresAt" LIMIT {batchSize} FOR UPDATE SKIP LOCKED)
            """, ct);
        var recovery = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "RecoveryThrottles" WHERE "Id" IN (
                SELECT "Id" FROM "RecoveryThrottles" WHERE "NextAllowedAt" <= {utcNow}
                ORDER BY "NextAllowedAt" LIMIT {batchSize} FOR UPDATE SKIP LOCKED)
            """, ct);
        return new SecurityThrottleCleanupResult(login, recovery);
    }
}

internal sealed record SecurityThrottleCleanupResult(int LoginDeleted, int RecoveryDeleted);

public sealed record SecurityThrottleCleanupOptions(TimeSpan SweepInterval, int BatchSize);
