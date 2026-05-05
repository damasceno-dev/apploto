using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Holidays.Update;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.Update;

public class UpdateHolidayUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateDescription_WhenManagerSubmitsValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription("Nova descrição").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(holiday.Id, request);

        response.Description.ShouldBe("Nova descrição");
        response.Id.ShouldBe(holiday.Id);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateDescription_WhenAdminSubmitsValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(holiday.Id, request);

        response.ShouldNotBeNull();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSetDescriptionToNull_WhenDescriptionIsWhitespace()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription("   ").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(holiday.Id, request);

        response.Description.ShouldBeNull();
    }

    [Fact]
    public async Task Execute_ShouldNotMutateDate_WhenUpdateIsApplied()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var originalDate = new DateTime(2025, 9, 7);
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).WithDate(originalDate).Build();
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription("New desc").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(holiday.Id, request);

        response.Date.ShouldBe(originalDate);
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenMemberAttempts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(Guid.NewGuid(), request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenHolidayNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var missingId = Guid.NewGuid();
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(missingId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(missingId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldPassCorrectBranchIdToRepository()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var holiday = new HolidayBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateHolidayJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .GetByIdAndBranchIdReturns(holiday.Id, branchUser.BranchId, holiday)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(holiday.Id, request);

        await holidaysRepository.Received(1).GetByIdAndBranchId(holiday.Id, branchUser.BranchId);
    }

    private static UpdateHolidayUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateHolidayUseCase(authenticationService, holidaysRepository, unitOfWork);
    }
}
