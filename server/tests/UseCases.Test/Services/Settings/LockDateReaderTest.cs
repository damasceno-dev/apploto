using CommonTestUtilities.Repositories;
using server.Application.Services.Settings;
using server.Domain.Entities;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Settings;

public class LockDateReaderTest
{
    [Fact]
    public async Task Read_ShouldReturnMinValueWhenSettingIsMissing()
    {
        var branchId = Guid.NewGuid();
        var reader = new LockDateReader(new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchId, null)
            .Build());

        (await reader.Read(branchId)).ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public async Task Read_ShouldReturnResolvedBranchLockDate()
    {
        var branchId = Guid.NewGuid();
        var lockDate = new DateTime(2026, 7, 31);
        var reader = new LockDateReader(new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                branchId,
                new Setting { BranchId = branchId, LockDate = lockDate })
            .Build());

        (await reader.Read(branchId)).ShouldBe(lockDate);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void ResolvedGuardOverload_ShouldMatchExclusiveUnlockRule(int targetOffset, bool allowed)
    {
        var lockDate = new DateTime(2026, 7, 31);
        var guard = new LockDateGuard(new LockDateReader(
            new SettingsRepositoryBuilder().Build()));

        if (allowed)
        {
            Should.NotThrow(() => guard.EnsureNotLocked(
                lockDate.AddDays(-targetOffset),
                lockDate,
                "locked"));
            return;
        }

        var exception = Should.Throw<ConflictException>(() => guard.EnsureNotLocked(
            lockDate.AddDays(-targetOffset),
            lockDate,
            "locked"));
        exception.Message.ShouldBe("locked");
    }
}
