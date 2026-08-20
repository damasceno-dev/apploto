using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IBranchesRepository
{
    Task Add(Branch branch);
    Task<Branch?> GetById(Guid branchId, CancellationToken ct = default);
}
