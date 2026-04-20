using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public class LockDateGuard(ISettingsRepository settingsRepository)
{
    public async Task EnsureNotLocked(Guid branchId, DateTime targetDate)
    {
        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchId);

        if (setting?.LockDate is { } lockDate && targetDate <= lockDate)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        }
    }
}
