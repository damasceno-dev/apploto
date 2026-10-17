using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IClientsRepository
{
    Task Add(Client client);
    Task<Client?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Client?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// A branch client regardless of its own <c>Active</c> flag: a deactivated client still carries its fiado
    /// history and balance, exactly as the fiado balance report counts it.
    /// </summary>
    Task<Client?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Client>> ListActiveByBranchId(Guid branchId);
    Task<Client?> GetActiveByCpfAndBranchId(string cpf, Guid branchId);
}
