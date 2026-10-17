namespace server.Domain.Interfaces;

/// <summary>
/// Serializes every write that changes a Terminal↔Tab pairing or deactivates an account in one
/// branch, so a concurrent pair and deactivation can never leave an inactive account holding a
/// pairing. The scope owns a transaction-scoped PostgreSQL advisory lock and rolls back unless
/// <see cref="IAccountPairingCoordinationScope.Complete"/> is called. Returns <c>null</c> when the
/// lock cannot be acquired within the configured timeout.
/// </summary>
public interface IAccountPairingCoordination
{
    Task<IAccountPairingCoordinationScope?> TryAcquire(Guid branchId, CancellationToken ct = default);
}

public interface IAccountPairingCoordinationScope : IAsyncDisposable
{
    Task Complete(CancellationToken ct = default);
}
