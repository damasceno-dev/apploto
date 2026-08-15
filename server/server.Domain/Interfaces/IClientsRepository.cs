using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IClientsRepository
{
    Task Add(Client client);
    Task<Client?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Client?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListActiveByBranchId(Guid branchId);
    Task<Client?> GetActiveByCpfAndBranchId(string cpf, Guid branchId);
}
