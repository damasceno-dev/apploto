using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Holidays;
using server.Application.UseCases.Holidays.PreviewBrazilianImport;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Holidays.PreviewBrazilianImport;

public class PreviewBrazilianHolidayImportUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnTenNationalEntriesWithAlreadyExistsFlags_WhenOptionalFederalIsFalse()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var existingDate = new DateOnly(2026, 1, 1);
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, [existingDate])
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(2026, includeOptionalFederal: false);

        response.Year.ShouldBe(2026);
        response.IncludesOptionalFederal.ShouldBeFalse();
        response.Items.Count.ShouldBe(10);
        response.Items.ShouldAllBe(item => item.Type == BrazilianHolidayType.National);
        response.Items.Single(item => item.Date == existingDate).AlreadyExists.ShouldBeTrue();
        response.Items.Where(item => item.Date != existingDate).ShouldAllBe(item => item.AlreadyExists == false);
        await holidaysRepository.Received(1).ListActiveDatesByBranchIdAndYearAsNoTracking(branchUser.BranchId, 2026);
    }

    [Fact]
    public async Task Execute_ShouldReturnThirteenEntriesWithMixedTypes_WhenOptionalFederalIsTrue()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var existingOptionalDate = new DateOnly(2026, 2, 17);
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, [existingOptionalDate])
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        var response = await useCase.Execute(2026, includeOptionalFederal: true);

        response.IncludesOptionalFederal.ShouldBeTrue();
        response.Items.Count.ShouldBe(13);
        response.Items.Count(item => item.Type == BrazilianHolidayType.National).ShouldBe(10);
        response.Items.Count(item => item.Type == BrazilianHolidayType.OptionalFederal).ShouldBe(3);
        response.Items.Single(item => item.Date == existingOptionalDate).AlreadyExists.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldUseAuthenticatedBranchOnly_WhenCheckingExistingDates()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var otherBranchId = Guid.NewGuid();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2027, [])
            .Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository);

        await useCase.Execute(2027, includeOptionalFederal: true);

        await holidaysRepository.Received(1).ListActiveDatesByBranchIdAndYearAsNoTracking(branchUser.BranchId, 2027);
        await holidaysRepository.DidNotReceive().ListActiveDatesByBranchIdAndYearAsNoTracking(otherBranchId, Arg.Any<int>());
    }

    private static PreviewBrazilianHolidayImportUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository)
    {
        return new PreviewBrazilianHolidayImportUseCase(
            authenticationService,
            holidaysRepository,
            new BrazilianHolidayCalendarResolverBuilder().Build());
    }
}
