using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Settings.Update;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Settings.Update;

public class UpdateSettingUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateAllMutableFields_WhenAdminUpdates()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateSettingJsonBuilder()
            .WithDailyTargetHours(8.0m)
            .WithLunchDeductionOver6H(1.5m)
            .WithLunchDeductionOver4H(0.5m)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var response = await useCase.Execute(request, 0);

        response.LockDate.ShouldBe(setting.LockDate);
        response.DailyTargetHours.ShouldBe(8.0m);
        response.LunchDeductionOver6H.ShouldBe(1.5m);
        response.LunchDeductionOver4H.ShouldBe(0.5m);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateOnlyDailyTargetHours_WhenOnlyThatFieldProvided()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDailyTargetHours(7.33m)
            .WithLunchDeductionOver6H(1.0m)
            .WithLunchDeductionOver4H(0.25m)
            .WithLockDate(DateTime.MinValue)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(9.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var response = await useCase.Execute(request, 0);

        response.DailyTargetHours.ShouldBe(9.0m);
        response.LunchDeductionOver6H.ShouldBe(1.0m);
        response.LunchDeductionOver4H.ShouldBe(0.25m);
        response.LockDate.ShouldBe(DateTime.MinValue);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateOnlyLunchDeductionOver6H_WhenOnlyThatFieldProvided()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithLunchDeductionOver6H(1.0m)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(2.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var response = await useCase.Execute(request, 0);

        response.LunchDeductionOver6H.ShouldBe(2.0m);
        response.DailyTargetHours.ShouldBe(setting.DailyTargetHours);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateOnlyLunchDeductionOver4H_WhenOnlyThatFieldProvided()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithLunchDeductionOver4H(0.25m)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver4H(0.5m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var response = await useCase.Execute(request, 0);

        response.LunchDeductionOver4H.ShouldBe(0.5m);
        response.LunchDeductionOver6H.ShouldBe(setting.LunchDeductionOver6H);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdate_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var setting = new SettingBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var response = await useCase.Execute(request, 0);

        response.DailyTargetHours.ShouldBe(8.0m);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request, 0));

        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowExactReadOnlyKey_WhenLockDateMovesBackward()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var lockDate = new DateTime(2026, 6, 1);
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithLockDate(lockDate)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder()
            .WithLockDate(lockDate.AddDays(-1))
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request, 0));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LOCK_DATE_READ_ONLY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowExactReadOnlyKey_WhenLockDateEqualsCurrentLockDate()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var lockDate = new DateTime(2026, 6, 1);
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithLockDate(lockDate)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder().WithLockDate(lockDate).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request, 0));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LOCK_DATE_READ_ONLY);
        setting.LockDate.ShouldBe(lockDate);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowExactReadOnlyKey_WhenLockDateMovesForward()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var lockDate = new DateTime(2026, 6, 1);
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithLockDate(lockDate)
            .Build();
        var newLockDate = lockDate.AddDays(30);
        var request = new RequestUpdateSettingJsonBuilder().WithLockDate(newLockDate).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request, 0));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_LOCK_DATE_READ_ONLY);
        setting.LockDate.ShouldBe(lockDate);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenDailyTargetHoursIsInvalid()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request, 0));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowInvalidOperationException_WhenSettingRowMissing()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => useCase.Execute(request, 0));

        exception.Message.ShouldContain(branchUser.BranchId.ToString());
        await unitOfWork.DidNotReceive().Commit();
    }

    // -------------------------------------------------------------------------
    // Effective-dated policy ledger (M7.7 Phase 7): a real constants change writes
    // the policy row effective from the branch-local day of the change.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldInsertPolicyEffectiveToday_WhenAnyConstantChanges()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDailyTargetHours(7.33m)
            .WithLunchDeductionOver6H(1.0m)
            .WithLunchDeductionOver4H(0.25m)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(8.0m).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var timeEntryPoliciesRepository = new TimeEntryPoliciesRepositoryBuilder()
            .GetActiveByBranchIdAndEffectiveFromReturns(branchUser.BranchId, LocalToday, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(
            authenticationService, settingsRepository, unitOfWork, timeEntryPoliciesRepository);

        await useCase.Execute(request, 0);

        // The inserted row mirrors the post-update Setting values: changed target plus the
        // untouched lunch tiers, effective from the branch-local day of the change.
        await timeEntryPoliciesRepository.Received(1).Add(Arg.Is<TimeEntryPolicy>(policy =>
            policy.BranchId == branchUser.BranchId &&
            policy.EffectiveFrom == LocalToday &&
            policy.DailyTargetHours == 8.0m &&
            policy.LunchDeductionOver6H == 1.0m &&
            policy.LunchDeductionOver4H == 0.25m));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldMutateSameDayPolicyRowInPlace_WhenTodayAlreadyChangedOnce()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder().WithBranchId(branchUser.BranchId).Build();
        var sameDayPolicy = new TimeEntryPolicyBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithEffectiveFrom(LocalToday)
            .WithDailyTargetHours(8.0m)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder()
            .WithDailyTargetHours(6.5m)
            .WithLunchDeductionOver4H(0.5m)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var timeEntryPoliciesRepository = new TimeEntryPoliciesRepositoryBuilder()
            .GetActiveByBranchIdAndEffectiveFromReturns(branchUser.BranchId, LocalToday, sameDayPolicy)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(
            authenticationService, settingsRepository, unitOfWork, timeEntryPoliciesRepository);

        await useCase.Execute(request, 0);

        // No second row for the same effective date — the tracked same-day row is updated.
        await timeEntryPoliciesRepository.DidNotReceive().Add(Arg.Any<TimeEntryPolicy>());
        sameDayPolicy.DailyTargetHours.ShouldBe(6.5m);
        sameDayPolicy.LunchDeductionOver6H.ShouldBe(setting.LunchDeductionOver6H);
        sameDayPolicy.LunchDeductionOver4H.ShouldBe(0.5m);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotWritePolicy_WhenRequestedValuesEqualCurrentConstants()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDailyTargetHours(7.33m)
            .WithLunchDeductionOver6H(1.0m)
            .WithLunchDeductionOver4H(0.25m)
            .Build();
        var request = new RequestUpdateSettingJsonBuilder()
            .WithDailyTargetHours(7.33m)
            .WithLunchDeductionOver6H(1.0m)
            .WithLunchDeductionOver4H(0.25m)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdReturns(branchUser.BranchId, setting)
            .Build();
        var timeEntryPoliciesRepository = new TimeEntryPoliciesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(
            authenticationService, settingsRepository, unitOfWork, timeEntryPoliciesRepository);

        await useCase.Execute(request, 0);

        // A value-identical PUT must not append policy history noise.
        await timeEntryPoliciesRepository.DidNotReceive().Add(Arg.Any<TimeEntryPolicy>());
        await timeEntryPoliciesRepository.DidNotReceive().GetActiveByBranchIdAndEffectiveFrom(
            Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).Commit();
    }

    private static readonly DateTime FixedUtcNow = new(2026, 8, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LocalToday = new(2026, 8, 10);

    private static UpdateSettingUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ISettingsRepository settingsRepository,
        IUnitOfWork unitOfWork,
        ITimeEntryPoliciesRepository? timeEntryPoliciesRepository = null)
    {
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDate(FixedUtcNow).Returns(LocalToday);

        return new UpdateSettingUseCase(
            authenticationService,
            settingsRepository,
            timeEntryPoliciesRepository ?? new TimeEntryPoliciesRepositoryBuilder().Build(),
            branchClock,
            unitOfWork);
    }
}
