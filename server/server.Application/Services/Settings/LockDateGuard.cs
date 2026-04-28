using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Settings;

public class LockDateGuard(ISettingsRepository settingsRepository)
{
    public async Task EnsureNotLocked(
        Guid branchId,
        DateTime targetDate,
        string conflictResourceKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchId);

        if (setting?.LockDate is { } lockDate && targetDate <= lockDate)
        {
            throw new ConflictException(conflictResourceKey);
        }
    }
}
