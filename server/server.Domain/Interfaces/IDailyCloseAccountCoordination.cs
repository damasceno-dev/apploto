namespace server.Domain.Interfaces;

/// <summary>
/// Serializes close-history and Terminal-ledger writes for one account. The scope owns a
/// transaction-scoped PostgreSQL advisory lock and rolls back unless <see cref="IDailyCloseAccountCoordinationScope.Complete"/>
/// is called.
/// </summary>
public interface IDailyCloseAccountCoordination
{
    Task<IDailyCloseAccountCoordinationScope> Acquire(Guid branchId, Guid accountId, CancellationToken ct = default);
}

public interface IDailyCloseAccountCoordinationScope : IAsyncDisposable
{
    Task Complete(CancellationToken ct = default);
}
