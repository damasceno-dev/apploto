namespace server.Domain.Interfaces;

/// <summary>Owns a database transaction and sorted, bounded locks. Complete only after IUnitOfWork.Commit.</summary>
public interface ISecurityCoordination
{
    Task<ISecurityScope?> TryAcquire(params string[] resources);

    /// <summary>
    /// Takes the resource's lock in shared mode inside the caller's already-open transaction and holds it until that
    /// transaction ends. Writers that <see cref="TryAcquire"/> the same resource wait for it, and it waits for them.
    /// </summary>
    Task HoldSharedInCurrentTransaction(string resource, CancellationToken ct = default);
}

public interface ISecurityScope : IAsyncDisposable
{
    Task Complete();
}
