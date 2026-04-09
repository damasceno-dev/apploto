using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IBranchUsersRepository
{
    Task Add(BranchUser branchUser);
    Task<BranchUser?> GetActiveById(Guid branchUserId);
    Task<BranchUser?> GetActiveByUserIdAndBranchId(Guid userId, Guid branchId);
    Task<BranchUser?> GetByUserIdAndBranchId(Guid userId, Guid branchId);
}
