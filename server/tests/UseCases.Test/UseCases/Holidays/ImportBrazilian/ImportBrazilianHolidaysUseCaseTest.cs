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

    [Fact]
    public async Task Execute_Composite_ShouldProduceTenRowsByDefault_AndStampTopLevelSource()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        // Default source param = Composite — exercises the default explicitly.
        var response = await useCase.Execute(2026, includeOptionalFederal: false);

        response.Source.ShouldBe(BrazilianHolidayCalendarSource.Composite);
        response.Items.Count.ShouldBe(10);
        response.ImportedCount.ShouldBe(10);
    }

    [Theory]
    [InlineData(BrazilianHolidayCalendarSource.Composite)]
    [InlineData(BrazilianHolidayCalendarSource.Canonical)]
    [InlineData(BrazilianHolidayCalendarSource.BrasilApi)]
    [InlineData(BrazilianHolidayCalendarSource.Nager)]
    public async Task Execute_ShouldEchoTopLevelSource_ForEverySupportedSource(BrazilianHolidayCalendarSource source)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(2026, includeOptionalFederal: false, source, CancellationToken.None);

        response.Source.ShouldBe(source);
    }

    [Fact]
    public async Task Execute_ShouldPersistPerRowSource_FromResolverEntries()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2026, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        // Resolver returns three entries with mixed Source tags — the use case must
        // propagate those tags both onto the persisted Holiday rows and onto the
        // response item Source field.
        var mixedEntries = new List<SourcedBrazilianHolidayEntry>
        {
            new(new DateOnly(2026, 1, 1), "Confraternização Universal", BrazilianHolidayType.National, HolidaySource.Nager),
            new(new DateOnly(2026, 4, 21), "Tiradentes", BrazilianHolidayType.National, HolidaySource.BrasilApi),
            new(new DateOnly(2026, 12, 25), "Natal", BrazilianHolidayType.National, HolidaySource.Canonical)
        };
        var resolver = new BrazilianHolidayCalendarResolverBuilder()
            .Returns(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Composite, mixedEntries)
            .Build();

        var useCase = new ImportBrazilianHolidaysUseCase(
            authenticationService,
            holidaysRepository,
            resolver,
            unitOfWork);

        var response = await useCase.Execute(2026, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Composite, CancellationToken.None);

        response.Items.Count.ShouldBe(3);
        response.Items.Single(i => i.Date == new DateOnly(2026, 1, 1)).Source.ShouldBe(HolidaySource.Nager);
        response.Items.Single(i => i.Date == new DateOnly(2026, 4, 21)).Source.ShouldBe(HolidaySource.BrasilApi);
        response.Items.Single(i => i.Date == new DateOnly(2026, 12, 25)).Source.ShouldBe(HolidaySource.Canonical);

        await holidaysRepository.Received(1).Add(Arg.Is<Holiday>(h => h.Date == new DateTime(2026, 1, 1) && h.Source == HolidaySource.Nager));
        await holidaysRepository.Received(1).Add(Arg.Is<Holiday>(h => h.Date == new DateTime(2026, 4, 21) && h.Source == HolidaySource.BrasilApi));
        await holidaysRepository.Received(1).Add(Arg.Is<Holiday>(h => h.Date == new DateTime(2026, 12, 25) && h.Source == HolidaySource.Canonical));
    }

    [Fact]
    public async Task Execute_Canonical_ShouldStampEveryItemAndRowSourceAsCanonical()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var holidaysRepository = new HolidaysRepositoryBuilder()
            .ListActiveDatesByBranchIdAndYearAsNoTrackingReturns(branchUser.BranchId, 2027, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, holidaysRepository, unitOfWork);

        var response = await useCase.Execute(2027, includeOptionalFederal: true, BrazilianHolidayCalendarSource.Canonical, CancellationToken.None);

        response.Items.Count.ShouldBe(13);
        response.Items.ShouldAllBe(i => i.Source == HolidaySource.Canonical);
        await holidaysRepository.Received(13).Add(Arg.Is<Holiday>(h => h.Source == HolidaySource.Canonical));
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenYearIsOutsideRange()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        // Use a real resolver here so we exercise the actual ArgumentOutOfRangeException path.
        var resolver = new server.Application.Services.Holidays.BrazilianHolidayCalendarResolver(
            new BrazilianHolidayCalendar(),
            new BrasilApiHolidayProviderBuilder().Build(),
            new NagerDateHolidayProviderBuilder().Build(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<server.Application.Services.Holidays.BrazilianHolidayCalendarResolver>.Instance);
        var holidaysRepository = new HolidaysRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new ImportBrazilianHolidaysUseCase(authenticationService, holidaysRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            useCase.Execute(1800, includeOptionalFederal: false, BrazilianHolidayCalendarSource.Canonical, CancellationToken.None));
        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static ImportBrazilianHolidaysUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IHolidaysRepository holidaysRepository,
        IUnitOfWork unitOfWork)
    {
        return new ImportBrazilianHolidaysUseCase(
            authenticationService,
            holidaysRepository,
            new BrazilianHolidayCalendarResolverBuilder().Build(),
            unitOfWork);
    }
}
