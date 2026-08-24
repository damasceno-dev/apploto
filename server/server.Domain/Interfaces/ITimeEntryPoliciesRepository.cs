using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITimeEntryPoliciesRepository
{
    Task Add(TimeEntryPolicy policy);
    Task<IReadOnlyList<TimeEntryPolicy>> ListActiveByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
    Task<TimeEntryPolicy?> GetActiveByBranchIdAndEffectiveFrom(Guid branchId, DateTime effectiveFrom, CancellationToken ct = default);
}
