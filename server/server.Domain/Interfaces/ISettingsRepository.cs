using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ISettingsRepository
{
    Task Add(Setting setting);
    Task<Setting?> GetByBranchId(Guid branchId);
    Task<Setting?> GetByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
}
