namespace server.Domain.Interfaces;

/// <summary>
/// Owns the branch-wide transaction boundary shared by month locking and every write whose
/// effective date is governed by <c>Setting.LockDate</c>. Shared holders may run concurrently;
/// the lock-month command takes the exclusive form.
/// </summary>
public interface IMonthLockCoordination
{
    Task<IMonthLockCoordinationScope?> TryAcquireShared(Guid branchId, CancellationToken ct = default);
    Task<IMonthLockCoordinationScope?> TryAcquireExclusive(Guid branchId, CancellationToken ct = default);
}

public interface IMonthLockCoordinationScope : IAsyncDisposable
{
    Task Complete(CancellationToken ct = default);
}
