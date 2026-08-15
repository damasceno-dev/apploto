using server.Domain.Interfaces;

namespace server.Application.Services.Settings;

public sealed class LockDateReader(ISettingsRepository settingsRepository) : ILockDateReader
{
    public async Task<DateTime> Read(Guid branchId, CancellationToken ct = default)
    {
        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchId, ct);
        return setting?.LockDate ?? DateTime.MinValue;
    }
}
