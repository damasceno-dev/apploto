using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.UseCases.DailyCloses.List;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.DailyCloses.List;

public class ListDailyClosesUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldPassResolvedFilterToListAndCount_AndUseCountForTotalCount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var dateFrom = new DateTime(2025, 3, 1);
        var dateTo = new DateTime(2025, 3, 31);
        var request = new RequestListDailyClosesJsonBuilder()
            .WithAccountId(accountId)
            .WithStatus(DailyCloseStatus.Draft)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithOperatorId(operatorId)
            .WithPage(2)
            .WithPageSize(3)
            .Build();
        var expectedFilter = new DailyCloseListFilter
        {
            AccountId = accountId,
            Status = DailyCloseStatus.Draft,
            DateFrom = dateFrom,
            DateTo = dateTo,
            OperatorId = operatorId,
            Page = 2,
            PageSize = 3
        };
        var closes = BuildCloses(branchUser.BranchId, accountId, 3);
        var ctx = BuildContext(branchUser);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, closes)
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 42)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(3);
        response.TotalCount.ShouldBe(42);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(3);
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
        await ctx.DailyClosesRepository.Received(1).CountByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldPassSameResolvedFilterInstanceToListAndCount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithAccountId(accountId)
            .WithPage(2)
            .WithPageSize(3)
            .Build();
        var dailyClosesRepository = Substitute.For<IDailyClosesRepository>();
        DailyCloseListFilter? listFilter = null;
        DailyCloseListFilter? countFilter = null;

        dailyClosesRepository
            .ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchUser.BranchId),
                Arg.Do<DailyCloseListFilter>(filter => listFilter = filter))
            .Returns([]);
        dailyClosesRepository
            .CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchUser.BranchId),
                Arg.Do<DailyCloseListFilter>(filter => countFilter = filter))
            .Returns(7);
        var ctx = BuildContext(branchUser);
        ctx.DailyClosesRepository = dailyClosesRepository;
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(7);
        listFilter.ShouldNotBeNull();
        countFilter.ShouldNotBeNull();
        listFilter.ShouldBeSameAs(countFilter);
        listFilter.AccountId.ShouldBe(accountId);
        listFilter.Page.ShouldBe(2);
        listFilter.PageSize.ShouldBe(3);
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepositoryCalls_WhenMemberHasNoLinkedOperatorAndNoExplicitAccountId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestListDailyClosesJsonBuilder().Build();
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
        await ctx.DailyClosesRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
        await ctx.DailyClosesRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepositoryCalls_WhenMemberHasLinkedOperatorButNoActiveAccountsAndNoExplicitAccountId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var request = new RequestListDailyClosesJsonBuilder().Build();
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [])
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
        await ctx.DailyClosesRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
        await ctx.DailyClosesRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldPopulateAccountAndOperatorNames_OnListItems()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var approvedByUser = new UserBuilder()
            .WithName("Ana Gerente")
            .Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Caixa Principal")
            .Build();
        var submittedByOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("João Operador")
            .Build();
        var dailyClose = new DailyCloseBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithSubmittedByOperator(submittedByOperator)
            .WithApprovedByUser(approvedByUser)
            .Build();
        var request = new RequestListDailyClosesJsonBuilder().Build();
        var expectedFilter = new DailyCloseListFilter { Page = 1, PageSize = 20 };
        var ctx = BuildContext(branchUser);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [dailyClose])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 1)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        var item = response.Items[0];
        item.AccountName.ShouldBe("Caixa Principal");
        item.SubmittedByOperatorName.ShouldBe("João Operador");
        item.ApprovedByUserName.ShouldBe("Ana Gerente");
    }

    [Theory]
    [InlineData(7, 3, 1, 3, true, false)]
    [InlineData(7, 3, 2, 3, true, true)]
    [InlineData(7, 3, 3, 3, false, true)]
    [InlineData(0, 10, 1, 0, false, false)]
    [InlineData(10, 10, 1, 1, false, false)]
    public async Task Execute_ShouldComputePagingMetadata(
        int totalCount,
        int pageSize,
        int page,
        int expectedTotalPages,
        bool expectedHasNext,
        bool expectedHasPrevious)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithPage(page)
            .WithPageSize(pageSize)
            .Build();
        var expectedFilter = new DailyCloseListFilter { Page = page, PageSize = pageSize };
        var ctx = BuildContext(branchUser);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, totalCount)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(totalCount);
        response.TotalPages.ShouldBe(expectedTotalPages);
        response.HasNext.ShouldBe(expectedHasNext);
        response.HasPrevious.ShouldBe(expectedHasPrevious);
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepositoryCalls_WhenMemberExplicitAccountIsUnlinked()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccountId = Guid.NewGuid();
        var unlinkedAccountId = Guid.NewGuid();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithAccountId(unlinkedAccountId)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(linkedAccountId).Build()])
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        await ctx.DailyClosesRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
        await ctx.DailyClosesRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<DailyCloseListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldSetAllowedAccountIds_WhenMemberListsWithoutExplicitAccount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccountIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var request = new RequestListDailyClosesJsonBuilder()
            .WithPage(1)
            .WithPageSize(20)
            .Build();
        var expectedFilter = new DailyCloseListFilter
        {
            AllowedAccountIds = linkedAccountIds,
            Page = 1,
            PageSize = 20
        };
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                linkedAccountIds
                    .Select(accountId => new OperatorAccountBuilder()
                        .WithOperator(callerOperator)
                        .WithAccountId(accountId)
                        .Build())
                    .ToList())
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(
                branchUser.BranchId,
                expectedFilter,
                BuildCloses(branchUser.BranchId, linkedAccountIds[0], 1))
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 1)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(1);
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
        await ctx.DailyClosesRepository.Received(1).CountByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldStripOperatorId_WhenMemberListsWithoutMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccountId = Guid.NewGuid();
        var ignoredOperatorId = Guid.NewGuid();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithOperatorId(ignoredOperatorId)
            .Build();
        var expectedFilter = new DailyCloseListFilter
        {
            AllowedAccountIds = [linkedAccountId],
            Page = 1,
            PageSize = 20
        };
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(linkedAccountId).Build()])
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldSetOperatorIdToCallerOperator_WhenMemberUsesMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var linkedAccountId = Guid.NewGuid();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithMine(true)
            .Build();
        var expectedFilter = new DailyCloseListFilter
        {
            OperatorId = callerOperator.Id,
            AllowedAccountIds = [linkedAccountId],
            Page = 1,
            PageSize = 20
        };
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(
                callerOperator.Id,
                [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccountId(linkedAccountId).Build()])
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldSetOperatorIdToCallerOperator_WhenManagerUsesMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var request = new RequestListDailyClosesJsonBuilder()
            .WithMine(true)
            .Build();
        var expectedFilter = new DailyCloseListFilter
        {
            OperatorId = callerOperator.Id,
            Page = 1,
            PageSize = 20
        };
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [])
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    private static ListDailyClosesUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);

        return new ListDailyClosesUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            memberAccountScopeResolver);
    }

    private static TestContext BuildContext(BranchUser branchUser)
    {
        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = new DailyClosesRepositoryBuilder().Build(),
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build()
        };
    }

    private static IReadOnlyList<DailyClose> BuildCloses(Guid branchId, Guid accountId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(_ => new DailyCloseBuilder()
                .WithBranchId(branchId)
                .WithAccountId(accountId)
                .Build())
            .ToList();
    }

    private static bool MatchesFilter(DailyCloseListFilter expected, DailyCloseListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.Status == actual.Status &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.OperatorId == actual.OperatorId &&
               MatchesAllowedAccountIds(expected.AllowedAccountIds, actual.AllowedAccountIds) &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }

    private static bool MatchesAllowedAccountIds(IReadOnlyList<Guid>? expected, IReadOnlyList<Guid>? actual)
    {
        return expected is null
            ? actual is null
            : actual is not null && expected.SequenceEqual(actual);
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
