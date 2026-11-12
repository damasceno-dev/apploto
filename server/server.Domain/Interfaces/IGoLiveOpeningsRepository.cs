using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IGoLiveOpeningsRepository
{
    Task<Guid?> GetSourceImportId(Guid branchId, Guid openingId, CancellationToken ct);
    Task<bool> HasImportedDebt(Guid branchId, Guid accountId, Guid clientId, CancellationToken ct);
    Task<IReadOnlyList<GoLiveOpening>> List(Guid branchId, CancellationToken ct);
    Task Add(GoLiveOpening opening, CancellationToken ct);
    Task<GoLiveOpening?> GetForUpdate(Guid branchId, Guid id, CancellationToken ct);
    Task<GoLiveOpening?> Get(Guid branchId, Guid id, CancellationToken ct);
    Task<bool> Exists(Guid branchId, string identity, CancellationToken ct);
    Task<IReadOnlyList<Transaction>> Rows(Guid branchId, Guid id, CancellationToken ct);
    Task<Guid?> GetClientCredit(Guid branchId, Guid clientId, CancellationToken ct);
    Task<bool> HasClientCredit(Guid branchId, Guid clientId, CancellationToken ct);
}
