using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Holidays.Deactivate;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.Deactivate;

public class DeactivateHolidayUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateHoliday_WhenManagerSubmitsValidId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).WithActive(true).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(holiday.Id);

        holiday.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDeactivateHoliday_WhenAdminSubmitsValidId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).WithActive(true).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(holiday.Id);

        holiday.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenMemberAttempts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenHolidayNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var missingId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(missingId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(missingId));

        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldPassCorrectBranchIdToRepository()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(holiday.Id);

        await holidaysRepository.Received(1).GetByIdAndBranchId(holiday.Id, branchUser.BranchId);
    }

    private static DeactivateHolidayUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateHolidayUseCase(authenticationService, holidaysRepository, unitOfWork);
    }
}
