using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class IdempotencyRequestCleanup(
    ServerDbContext dbContext,
    IIdempotencyRequestsRepository requestsRepository,
    ILogger<IdempotencyRequestCleanup> logger)
{
    private static readonly EventId CompletedEvent = new(7711, "IdempotencyRequestCleanupCompleted");

    public async Task<IdempotencyRequestCleanupResult> DeleteExpiredBatch(
        DateTime utcNow,
        int batchSize,
        CancellationToken ct = default)
    {
        var candidates = await requestsRepository.ListExpiredScopesAsNoTracking(utcNow, batchSize, ct);
        if (candidates.Count == 0)
            return new IdempotencyRequestCleanupResult(0, 0);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
        var deletedCount = 0;

        foreach (var candidate in candidates)
        {
            await requestsRepository.AcquireScopeLock(
                candidate.Endpoint,
                candidate.BranchId,
                candidate.UserId,
                candidate.Key,
                ct);
            var current = await requestsRepository.GetByScope(
                candidate.Endpoint,
                candidate.BranchId,
                candidate.UserId,
                candidate.Key,
                ct);
            if (current is null || current.ExpiresAt > utcNow)
                continue;

            requestsRepository.Remove(current);
            deletedCount++;
        }

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation(
            CompletedEvent,
            "Deleted {DeletedCount} expired idempotency requests from a batch of {CandidateCount} candidates.",
            deletedCount,
            candidates.Count);
        return new IdempotencyRequestCleanupResult(candidates.Count, deletedCount);
    }
}

internal sealed record IdempotencyRequestCleanupResult(
    int CandidateCount,
    int DeletedCount);
