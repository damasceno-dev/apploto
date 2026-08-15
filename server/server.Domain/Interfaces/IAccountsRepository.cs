using server.Domain.Entities;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface IAccountsRepository
{
    Task Add(Account account);
    Task<Account?> GetActiveByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    Task<Account?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<Guid?> GetActiveTerminalIdByTabAccountId(Guid tabAccountId, Guid branchId);
    Task<IReadOnlyList<Account>> ListActiveByBranchId(Guid branchId);
    Task<IReadOnlyDictionary<Guid, Guid>> ListActiveTerminalIdsByTabAccountIds(Guid branchId, IReadOnlyCollection<Guid> tabAccountIds);
    /// <summary>
    /// Accounts expected to submit a daily close: active Terminal accounts with at least one active
    /// <c>OperatorAccount</c> link to an active <c>Operator</c>. Carries the primary linked operator
    /// (falling back to the first active link by operator name). Ordered AccountName ASC, then Id ASC.
    /// </summary>
    Task<IReadOnlyList<ExpectedCloserRow>> ListExpectedClosersByBranchIdAsNoTracking(
        Guid branchId, CancellationToken ct = default);
}
