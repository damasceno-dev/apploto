using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ISettingsRepository
{
    Task Add(Setting setting);
    Task<Setting?> GetByBranchIdAsNoTracking(Guid branchId);
}
