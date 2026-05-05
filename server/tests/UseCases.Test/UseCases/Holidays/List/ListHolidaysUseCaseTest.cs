using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Holidays.List;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.List;

public class ListHolidaysUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldPassResolvedFilterToListAndCount_AndUseCountForTotalCount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = new DateTime(2025, 12, 31);
        var request = new RequestListHolidaysJsonBuilder()
            .WithYear(2025)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithPage(2)
            .WithPageSize(5)
            .Build();
        var expectedFilter = new HolidayListFilter
        {
            Year = 2025,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Page = 2,
            PageSize = 5
        };
        var holidays = Enumerable.Range(1, 5)
            .Select(_ => new HolidayBuilder().WithBranchId(branchUser.BranchId).Build())
            .ToList();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, holidays)
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 42)
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(5);
        response.TotalCount.ShouldBe(42);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(5);
        await holidaysRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<HolidayListFilter>(f =>
                f.Year == 2025 &&
                f.DateFrom == dateFrom &&
                f.DateTo == dateTo &&
                f.Page == 2 &&
                f.PageSize == 5));
        await holidaysRepository.Received(1).CountByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<HolidayListFilter>(f =>
                f.Year == 2025 &&
                f.DateFrom == dateFrom &&
                f.DateTo == dateTo &&
                f.Page == 2 &&
                f.PageSize == 5));
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyItems_WhenBranchHasNoHolidays()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestListHolidaysJsonBuilder().Build();
        var expectedFilter = new HolidayListFilter { Page = 1, PageSize = 20 };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ShouldAllowMemberToList()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestListHolidaysJsonBuilder().Build();
        var expectedFilter = new HolidayListFilter { Page = 1, PageSize = 20 };
        var holidays = new List<server.Domain.Entities.Holiday> { new HolidayBuilder().WithBranchId(branchUser.BranchId).Build() };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, holidays)
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 1)
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        await holidaysRepository.Received(1).ListByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<HolidayListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldPassCorrectBranchIdToRepository_NotAnotherBranchId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestListHolidaysJsonBuilder().Build();
        var expectedFilter = new HolidayListFilter { Page = 1, PageSize = 20 };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        await useCase.Execute(request);

        await holidaysRepository.Received(1).ListByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<HolidayListFilter>());
        await holidaysRepository.DidNotReceive().ListByBranchIdAsNoTracking(
            Arg.Is<Guid>(id => id != branchUser.BranchId), Arg.Any<HolidayListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldComputePaginationMetadata()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestListHolidaysJsonBuilder()
            .WithPage(2)
            .WithPageSize(3)
            .Build();
        var expectedFilter = new HolidayListFilter { Page = 2, PageSize = 3 };
        var holidays = Enumerable.Range(1, 3)
            .Select(_ => new HolidayBuilder().WithBranchId(branchUser.BranchId).Build())
            .ToList();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, holidays)
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 7)
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasNext.ShouldBeTrue();
        response.HasPrevious.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenPageIsZero()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestListHolidaysJsonBuilder()
            .WithPage(0)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_LIST_PAGE_INVALID);
        await holidaysRepository.DidNotReceive().ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<HolidayListFilter>());
    }

    private static ListHolidaysUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository)
    {
        return new ListHolidaysUseCase(authenticationService, holidaysRepository);
    }
}
