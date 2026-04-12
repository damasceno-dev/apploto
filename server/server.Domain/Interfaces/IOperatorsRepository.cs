using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IOperatorsRepository
{
    Task Add(Operator op);
    Task<Operator?> GetActiveByIdAndBranchId(Guid id, Guid branchId);
    Task<IReadOnlyList<Operator>> ListActiveByBranchId(Guid branchId);
}
