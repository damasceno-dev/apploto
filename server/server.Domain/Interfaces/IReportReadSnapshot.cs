namespace server.Domain.Interfaces;

/// <summary>
/// One read-only PostgreSQL transaction at <c>REPEATABLE READ</c> on the request's database context (M11 Phase 7.1):
/// every query run while the scope is open sees the same committed state. Disposing the scope rolls it back; it never
/// commits, and the database refuses any write attempted inside it.
/// </summary>
public interface IReportReadSnapshot
{
    Task<IAsyncDisposable> Begin(CancellationToken ct = default);
}
