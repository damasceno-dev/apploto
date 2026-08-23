using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Infrastructure.Services;

namespace server.Infrastructure.Repositories;

internal sealed class IdempotencyRequestsRepository(ServerDbContext dbContext) : IIdempotencyRequestsRepository
{
    public async Task AcquireScopeLock(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Idempotency scope lock requires an active database transaction.");

        var lockKey = IdempotencyRequestLockKey.Compute(endpoint, branchId, userId, key);
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({lockKey})",
            ct);
    }

    public async Task<bool> TryAcquireScopeLock(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default)
    {
        try
        {
            await AcquireScopeLock(endpoint, branchId, userId, key, ct);
            return true;
        }
        catch (Exception exception) when (IsLockTimeout(exception))
        {
            return false;
        }
    }

    public async Task<IdempotencyRequest?> GetByScope(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default)
    {
        return await dbContext.IdempotencyRequests.FirstOrDefaultAsync(request =>
            request.Endpoint == endpoint &&
            request.BranchId == branchId &&
            request.UserId == userId &&
            request.Key == key,
            ct);
    }

    public async Task<IdempotencyRequest?> GetByScopeAsNoTracking(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default)
    {
        return await dbContext.IdempotencyRequests.AsNoTracking().FirstOrDefaultAsync(request =>
            request.Endpoint == endpoint &&
            request.BranchId == branchId &&
            request.UserId == userId &&
            request.Key == key,
            ct);
    }

    public async Task<IReadOnlyList<IdempotencyRequestScope>> ListExpiredScopesAsNoTracking(
        DateTime utcNow,
        int maxRows,
        CancellationToken ct = default)
    {
        return await dbContext.IdempotencyRequests
            .AsNoTracking()
            .Where(request => request.ExpiresAt <= utcNow)
            .OrderBy(request => request.ExpiresAt)
            .ThenBy(request => request.Id)
            .Take(maxRows)
            .Select(request => new IdempotencyRequestScope(
                request.Endpoint,
                request.BranchId,
                request.UserId,
                request.Key))
            .ToListAsync(ct);
    }

    public async Task Add(IdempotencyRequest request, CancellationToken ct = default)
    {
        await dbContext.IdempotencyRequests.AddAsync(request, ct);
    }

    public void Remove(IdempotencyRequest request)
    {
        dbContext.IdempotencyRequests.Remove(request);
    }

    private static bool IsLockTimeout(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable })
                return true;
        }

        return false;
    }
}
