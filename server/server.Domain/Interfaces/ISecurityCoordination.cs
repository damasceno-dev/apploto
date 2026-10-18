namespace server.Domain.Interfaces;

/// <summary>Owns a database transaction and sorted, bounded locks. Complete only after IUnitOfWork.Commit.</summary>
public interface ISecurityCoordination
{
    Task<ISecurityScope?> TryAcquire(params string[] resources);
}

public interface ISecurityScope : IAsyncDisposable
{
    Task Complete();
}
