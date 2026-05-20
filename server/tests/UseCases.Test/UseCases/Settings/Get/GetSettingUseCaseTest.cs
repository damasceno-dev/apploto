using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using server.Application.UseCases.Settings.Get;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Settings.Get;

public class GetSettingUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnSetting_WhenSettingExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var setting = new SettingBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository);

        var response = await useCase.Execute();

        response.Id.ShouldBe(setting.Id);
        response.LockDate.ShouldBe(setting.LockDate);
        response.DailyTargetHours.ShouldBe(setting.DailyTargetHours);
        response.LunchDeductionOver6H.ShouldBe(setting.LunchDeductionOver6H);
        response.LunchDeductionOver4H.ShouldBe(setting.LunchDeductionOver4H);
        response.BranchId.ShouldBe(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldReturnSetting_WhenCallerIsAdmin()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var setting = new SettingBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository);

        var response = await useCase.Execute();

        response.BranchId.ShouldBe(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowInvalidOperationException_WhenSettingRowMissing()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, settingsRepository);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => useCase.Execute());

        exception.Message.ShouldContain(branchUser.BranchId.ToString());
    }

    private static GetSettingUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ISettingsRepository settingsRepository)
    {
        return new GetSettingUseCase(authenticationService, settingsRepository);
    }
}
