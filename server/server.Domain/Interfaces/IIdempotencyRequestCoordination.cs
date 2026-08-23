namespace server.Domain.Interfaces;

/// <summary>
/// Owns the short transaction used to serialize a committed idempotency replay lookup by
/// endpoint, branch, user, and key without acquiring financial-command account coordination.
/// </summary>
public interface IIdempotencyRequestCoordination
{
    /// <returns>
    /// A transaction scope when the advisory key is acquired; otherwise <see langword="null"/>
    /// when PostgreSQL ends the lock wait at its configured timeout.
    /// </returns>
    Task<IIdempotencyRequestCoordinationScope?> TryAcquire(
        string endpoint,
        Guid branchId,
        Guid userId,
        string key,
        CancellationToken ct = default);
}

public interface IIdempotencyRequestCoordinationScope : IAsyncDisposable
{
    Task Complete(CancellationToken ct = default);
}
