using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatorsRepository
{
    Task Add(Operator op);
    Task<Operator?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Operator?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// A branch-owned operator whether active or deactivated (M11 Phase 2.4 historical drill-downs); null only for a
    /// missing or cross-branch id.
    /// </summary>
    Task<Operator?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Operator>> ListActiveByBranchId(Guid branchId);
    Task<Operator?> GetActiveLinkedByUserIdAndBranchIdAsNoTracking(Guid userId, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Operator>> ListByUserIdAndBranchId(Guid userId, Guid branchId, CancellationToken ct = default);
    Task<bool> ExistsActiveLinkedByUserIdAndBranchId(Guid userId,Guid branchId,Guid? exceptOperatorId = null);
}
