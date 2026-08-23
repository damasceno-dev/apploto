using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class IdempotencyRequestCoordination(
    ServerDbContext dbContext,
    IIdempotencyRequestsRepository requestsRepository,
    IdempotencyRequestCoordinationOptions options)
    : IIdempotencyRequestCoordination
{
    public async Task<IIdempotencyRequestCoordinationScope?> TryAcquire(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var lockTimeoutMilliseconds = checked((long)Math.Ceiling(options.LockTimeout.TotalMilliseconds));
            if (lockTimeoutMilliseconds <= 0)
                throw new InvalidOperationException("Idempotency coordination lock timeout must be positive.");

            await dbContext.Database.ExecuteSqlRawAsync(
                "SELECT set_config('lock_timeout', {0}, true)",
                [$"{lockTimeoutMilliseconds}ms"],
                ct);

            if (await requestsRepository.TryAcquireScopeLock(endpoint, branchId, userId, key, ct) is false)
            {
                await transaction.DisposeAsync();
                return null;
            }

            return new CoordinationScope(transaction);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    private sealed class CoordinationScope(IDbContextTransaction transaction)
        : IIdempotencyRequestCoordinationScope
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
