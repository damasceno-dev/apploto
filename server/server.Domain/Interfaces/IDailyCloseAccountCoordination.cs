namespace server.Domain.Interfaces;

/// <summary>
/// Serializes close-history and Terminal-ledger writes for one account. The scope owns a
/// transaction-scoped PostgreSQL advisory lock and rolls back unless <see cref="IDailyCloseAccountCoordinationScope.Complete"/>
/// is called.
/// </summary>
public interface IDailyCloseAccountCoordination
{
    Task<IDailyCloseAccountCoordinationScope> Acquire(Guid branchId, Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// One scope over several accounts of the branch (M7.7 Phase 5.6 physical fiado pairs: the Tab and its paired
    /// Terminal). The month-lock boundary is taken once, then every account key in ascending key order, so any two
    /// multi-account scopes — and any single-account scope — acquire in one global order and cannot deadlock.
    /// </summary>
    Task<IDailyCloseAccountCoordinationScope> AcquireMany(
        Guid branchId,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken ct = default);
}

public interface IDailyCloseAccountCoordinationScope : IAsyncDisposable
{
    Task Complete(CancellationToken ct = default);
}
