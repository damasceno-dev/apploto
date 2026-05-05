using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Holidays.Create;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.Create;

public class CreateHolidaysUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateHolidays_WhenManagerSubmitsValidBatch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = new DateTime(2025, 9, 7), Description = "Independência" },
                new RequestCreateHolidayJson { Date = new DateTime(2025, 11, 2), Description = "Finados" }
            ])
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(branchUser.BranchId, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(2);
        response.Items.All(h => h.BranchId == branchUser.BranchId).ShouldBeTrue();
        response.Items.All(h => h.Active).ShouldBeTrue();
        response.Items.Select(h => h.Date).ShouldContain(new DateTime(2025, 9, 7));
        response.Items.Select(h => h.Date).ShouldContain(new DateTime(2025, 11, 2));
        await holidaysRepository.Received(1).ListActiveDatesByBranchIdAsNoTracking(branchUser.BranchId);
        await holidaysRepository.Received(2).Add(Arg.Is<Holiday>(h => h.BranchId == branchUser.BranchId));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateHolidays_WhenAdminSubmitsValidBatch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(branchUser.BranchId, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Items.ShouldNotBeEmpty();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldStoreNullDescription_WhenDescriptionIsWhitespace()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(new DateTime(2025, 9, 7), "   ")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(branchUser.BranchId, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Items[0].Description.ShouldBeNull();
        await holidaysRepository.Received(1).Add(Arg.Is<Holiday>(h => h.Description == null));
    }

    [Fact]
    public async Task Execute_ShouldPassCorrectBranchIdToRepository()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(branchUser.BranchId, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(request);

        await holidaysRepository.Received(1).ListActiveDatesByBranchIdAsNoTracking(branchUser.BranchId);
        await holidaysRepository.Received(1).Add(Arg.Is<Holiday>(h => h.BranchId == branchUser.BranchId));
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenMemberAttempts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestCreateHolidaysJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await holidaysRepository.DidNotReceive().Add(Arg.Any<Holiday>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenDuplicateDatesInRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var duplicateDate = new DateTime(2025, 9, 7);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithHolidays([
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "First" },
                new RequestCreateHolidayJson { Date = duplicateDate, Description = "Second" }
            ])
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(branchUser.BranchId, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
        await holidaysRepository.DidNotReceive().Add(Arg.Any<Holiday>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenDateAlreadyExistsInBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var existingDate = new DateTime(2025, 9, 7);
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithSingleHoliday(existingDate, "Independência")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId,
                [DateOnly.FromDateTime(existingDate)])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
        await holidaysRepository.DidNotReceive().Add(Arg.Any<Holiday>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenBatchIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateHolidaysJsonBuilder()
            .WithEmptyList()
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_CREATE_BATCH_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateHolidaysUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateHolidaysUseCase(authenticationService, holidaysRepository, unitOfWork);
    }
}
