using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IAccountsRepository
{
    Task Add(Account account);
    Task<Account?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Account?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<Guid?> GetActiveTerminalIdByTabAccountId(Guid tabAccountId, Guid branchId);
    Task<IReadOnlyList<Account>> ListActiveByBranchId(Guid branchId);
    Task<IReadOnlyDictionary<Guid, Guid>> ListActiveTerminalIdsByTabAccountIds(Guid branchId, IReadOnlyCollection<Guid> tabAccountIds);
}
