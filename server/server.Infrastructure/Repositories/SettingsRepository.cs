using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class SettingsRepository(ServerDbContext dbContext) : ISettingsRepository
{
    public async Task Add(Setting setting)
    {
        await dbContext.Settings.AddAsync(setting);
    }

    public async Task<Setting?> GetByBranchId(Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Settings
            .FirstOrDefaultAsync(setting => setting.BranchId == branchId, ct);
    }

    public async Task<Setting?> GetByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(setting => setting.BranchId == branchId, ct);
    }
}
