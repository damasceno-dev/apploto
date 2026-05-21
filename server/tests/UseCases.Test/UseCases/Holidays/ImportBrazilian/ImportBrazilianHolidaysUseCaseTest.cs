using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Holidays;
using server.Application.UseCases.Holidays.ImportBrazilian;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.ImportBrazilian;

public class ImportBrazilianHolidaysUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnMixedImportedAndSkippedItems_WhenSomeDatesAlreadyExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var existingDates = new List<DateOnly>
        {
            new(2026, 1, 1),
            new(2026, 2, 17)
        };
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, existingDates)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(2026, includeOptionalFederal: true);

        response.Items.Count.ShouldBe(13);
        response.ImportedCount.ShouldBe(11);
        response.SkippedCount.ShouldBe(2);
        response.Items.Single(item => item.Date == new DateOnly(2026, 1, 1)).Status.ShouldBe(BrazilianHolidayImportStatus.Skipped);
        response.Items.Single(item => item.Date == new DateOnly(2026, 2, 17)).Status.ShouldBe(BrazilianHolidayImportStatus.Skipped);
        await holidaysRepository.Received(11).Add(Arg.Is<Holiday>(holiday => holiday.BranchId == branchUser.BranchId));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldImportEveryNationalHoliday_WhenBranchHasNoExistingDatesAndOptionalFederalIsFalse()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2027, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(2027, includeOptionalFederal: false);

        response.Items.Count.ShouldBe(10);
        response.ImportedCount.ShouldBe(10);
        response.SkippedCount.ShouldBe(0);
        response.Items.ShouldAllBe(item => item.Status == BrazilianHolidayImportStatus.Imported);
        response.Items.ShouldAllBe(item => item.Type == BrazilianHolidayType.National);
        await holidaysRepository.Received(10).Add(Arg.Is<Holiday>(holiday => holiday.BranchId == branchUser.BranchId));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldSkipEveryItemAndNotAdd_WhenAllDatesAlreadyExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var existingDates = new BrazilianHolidayCalendar()
            .GetForYear(2028, includeOptionalFederal: true)
            .Select(entry => entry.Date)
            .ToList();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2028, existingDates)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(2028, includeOptionalFederal: true);

        response.ImportedCount.ShouldBe(0);
        response.SkippedCount.ShouldBe(13);
        response.Items.ShouldAllBe(item => item.Status == BrazilianHolidayImportStatus.Skipped);
        await holidaysRepository.DidNotReceive().Add(Arg.Any<Holiday>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenCalledTwiceInSequence()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var existingDates = new HashSet<DateOnly>();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = Substitute.For<IHolidaysRepository>();
        holidaysRepository
            .ListActiveDatesByBranchIdAndYearAsNoTracking(branchUser.BranchId, 2029)
            .Returns(_ => existingDates.ToList());
        holidaysRepository
            .When(repository => repository.Add(Arg.Any<Holiday>()))
            .Do(call => existingDates.Add(DateOnly.FromDateTime(call.Arg<Holiday>().Date)));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var firstResponse = await useCase.Execute(2029, includeOptionalFederal: false);
        var secondResponse = await useCase.Execute(2029, includeOptionalFederal: false);

        firstResponse.ImportedCount.ShouldBe(10);
        firstResponse.SkippedCount.ShouldBe(0);
        secondResponse.ImportedCount.ShouldBe(0);
        secondResponse.SkippedCount.ShouldBe(10);
        secondResponse.Items.ShouldAllBe(item => item.Status == BrazilianHolidayImportStatus.Skipped);
        await holidaysRepository.Received(10).Add(Arg.Any<Holiday>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(2026, includeOptionalFederal: true));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await holidaysRepository.DidNotReceive().Add(Arg.Any<Holiday>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUseAuthenticatedBranchOnly_WhenImporting()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var otherBranchId = Guid.NewGuid();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2030, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        await useCase.Execute(2030, includeOptionalFederal: false);

        await holidaysRepository.Received(1).ListActiveDatesByBranchIdAndYearAsNoTracking(branchUser.BranchId, 2030);
        await holidaysRepository.DidNotReceive().ListActiveDatesByBranchIdAndYearAsNoTracking(otherBranchId, Arg.Any<int>());
        await holidaysRepository.DidNotReceive().Add(Arg.Is<Holiday>(holiday => holiday.BranchId == otherBranchId));
    }

    private static ImportBrazilianHolidaysUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository,
        IUnitOfWork unitOfWork)
    {
        return new ImportBrazilianHolidaysUseCase(
            authenticationService,
            holidaysRepository,
            new BrazilianHolidayCalendar(),
            unitOfWork);
    }
}
