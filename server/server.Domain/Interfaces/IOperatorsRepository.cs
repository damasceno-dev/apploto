using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatorsRepository
{
    Task Add(Operator op);
    Task<Operator?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<Operator?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Operator>> ListActiveByBranchId(Guid branchId);
    Task<Operator?> GetActiveLinkedByUserIdAndBranchIdAsNoTracking(Guid userId, Guid branchId, CancellationToken ct = default);
    Task<bool> ExistsActiveLinkedByUserIdAndBranchId(Guid userId,Guid branchId,Guid? exceptOperatorId = null);
}
