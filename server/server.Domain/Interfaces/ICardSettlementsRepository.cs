using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ICardSettlementsRepository
{
    Task<IReadOnlyList<CardProvider>> ListProvidersAsNoTracking(Guid branchId, CancellationToken ct = default);
    Task AddProvider(CardProvider provider, CancellationToken ct = default);
    Task<CardProvider?> GetProviderAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task LockProviderIdentity(Guid branchId, string identity, CancellationToken ct = default);
    Task<bool> ProviderIdentityExists(Guid branchId, string identity, CancellationToken ct = default);
    Task<CardSettlementBatch?> GetBatchAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task AddFeeEvidence(CardSettlementFeeEvidence evidence, CancellationToken ct = default);
    Task AddBatch(CardSettlementBatch batch, CancellationToken ct = default);
    Task<bool> BatchIdentityExists(Guid providerId, string identity, CancellationToken ct = default);
}
