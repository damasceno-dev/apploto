using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TimeEntryPoliciesRepository(ServerDbContext dbContext) : ITimeEntryPoliciesRepository
{
    public async Task Add(TimeEntryPolicy policy)
    {
        await dbContext.TimeEntryPolicies.AddAsync(policy);
    }

    public async Task<IReadOnlyList<TimeEntryPolicy>> ListActiveByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.TimeEntryPolicies
            .AsNoTracking()
            .Where(policy => policy.BranchId == branchId && policy.Active)
            .OrderBy(policy => policy.EffectiveFrom)
            .ThenBy(policy => policy.CreatedAt)
            .ThenBy(policy => policy.Id)
            .ToListAsync(ct);
    }

    public async Task<TimeEntryPolicy?> GetActiveByBranchIdAndEffectiveFrom(
        Guid branchId,
        DateTime effectiveFrom,
        CancellationToken ct = default)
    {
        return await dbContext.TimeEntryPolicies
            .FirstOrDefaultAsync(policy =>
                policy.BranchId == branchId &&
                policy.EffectiveFrom == effectiveFrom &&
                policy.Active, ct);
    }
}
