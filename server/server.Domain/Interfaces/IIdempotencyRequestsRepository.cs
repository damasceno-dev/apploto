using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface IIdempotencyRequestsRepository
{
    Task AcquireScopeLock(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default);

    Task<bool> TryAcquireScopeLock(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default);

    Task<IdempotencyRequest?> GetByScope(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default);

    Task<IdempotencyRequest?> GetByScopeAsNoTracking(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default);

    Task<IReadOnlyList<IdempotencyRequestScope>> ListExpiredScopesAsNoTracking(
        DateTime utcNow,
        int maxRows,
        CancellationToken ct = default);

    Task Add(IdempotencyRequest request, CancellationToken ct = default);
    void Remove(IdempotencyRequest request);
}
