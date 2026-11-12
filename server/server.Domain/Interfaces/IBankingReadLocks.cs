namespace server.Domain.Interfaces;

/// <summary>Hold validated account, source and configuration rows stable through an owned financial transaction.</summary>
public interface IBankingReadLocks
{
    Task Clients(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task Operators(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task Accounts(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task Transactions(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
    Task TransactionTypes(Guid branchId, IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
