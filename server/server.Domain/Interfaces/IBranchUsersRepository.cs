using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IBranchUsersRepository
{
    Task Add(BranchUser branchUser);
    Task<BranchUser?> GetById(Guid branchUserId);
    Task<BranchUser?> GetActiveById(Guid branchUserId);
    Task<BranchUser?> GetActiveByUserIdAndBranchId(Guid userId, Guid branchId);
    Task<BranchUser?> GetByUserIdAndBranchId(Guid userId, Guid branchId);
    Task<int> CountActiveAdminsByBranchId(Guid branchId);

    /// <summary>
    /// Locks every membership row of the branch — active or not — and its user FOR SHARE inside the caller's
    /// transaction, so a reactivation, role change or deactivation of an existing row waits for the caller's commit.
    /// New memberships are covered by the branch security lock, which every membership insert takes.
    /// </summary>
    Task LockMembershipsByBranchId(Guid branchId, CancellationToken ct = default);

    /// <summary>
    /// Counts the active Manager/Admin memberships backed by an active user. A write decision must hold the branch
    /// security lock first, so no membership insert, reactivation, role change or removal lands before its commit.
    /// </summary>
    Task<int> CountActiveManagersByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<BranchUser>> ListActiveByBranchId(Guid branchId);
    Task<IReadOnlyList<BranchUser>> ListActiveByUserId(Guid userId);
}
