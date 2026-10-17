namespace server.Domain.Interfaces;

public interface IUnitOfWork
{
    Task Commit(CancellationToken ct = default);

    /// <summary>
    /// Commits like <see cref="Commit"/>, but reports an optimistic-concurrency conflict (a versioned root
    /// changed since it was read) as <c>false</c> instead of throwing, so a command can name its own stale
    /// error. Nothing is persisted when it returns <c>false</c>.
    /// </summary>
    Task<bool> TryCommitOptimistic(CancellationToken ct = default);
}
