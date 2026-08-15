using server.Exceptions.Exceptions;

namespace server.Application.Services.Settings;

public class LockDateGuard(ILockDateReader lockDateReader)
{
    public async Task EnsureNotLocked(
        Guid branchId,
        DateTime targetDate,
        string conflictResourceKey,
        CancellationToken ct = default)
    {
        var lockDate = await lockDateReader.Read(branchId, ct);
        EnsureNotLocked(targetDate, lockDate, conflictResourceKey);
    }

    public void EnsureNotLocked(DateTime targetDate, DateTime resolvedLockDate, string conflictResourceKey)
    {
        if (targetDate <= resolvedLockDate)
            throw new ConflictException(conflictResourceKey);
    }
}
