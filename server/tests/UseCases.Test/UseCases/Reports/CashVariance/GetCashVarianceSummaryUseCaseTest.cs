using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.Reports.CashVariance;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.CashVariance;

public class GetCashVarianceSummaryUseCaseTest
{
    private static readonly DateTime DateFrom = new(2025, 1, 1);
    private static readonly DateTime DateTo = new(2025, 1, 31);

    // -------------------------------------------------------------------------
    // Happy: mixed-status closes — variance read from persisted item
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnAllRows_WhenClosesHaveMixedStatuses()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();

        var rows = new List<VarianceTimeSeriesRow>
        {
            new(DateFrom, Guid.NewGuid(), "Account A", 100m, DailyCloseStatus.Approved),
            new(DateFrom.AddDays(1), Guid.NewGuid(), "Account B", -50m, DailyCloseStatus.Submitted),
            new(DateFrom.AddDays(2), Guid.NewGuid(), "Account C", 200m, DailyCloseStatus.Draft)
        };

        var ctx = BuildContext(branchUser, productId, rows: rows);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(3);
        response.Items.Count.ShouldBe(3);
        response.TotalVariance.ShouldBe(250m);
        response.MeanVariance.ShouldBe(250m / 3m);
        response.MaxVariance.ShouldBe(200m);
        response.MinVariance.ShouldBe(-50m);
        response.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Approved).ShouldBeTrue();
        response.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Submitted).ShouldBeTrue();
        response.Items.Any(i => i.DailyCloseStatus == DailyCloseStatus.Draft).ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Happy: empty window → all aggregates zero and Items = []
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnZeroAggregatesAndEmptyItems_WhenNoRowsInWindow()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, productId, rows: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        response.Items.ShouldBeEmpty();
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
        response.TotalVariance.ShouldBe(0m);
        response.MeanVariance.ShouldBe(0m);
        response.MaxVariance.ShouldBe(0m);
        response.MinVariance.ShouldBe(0m);
    }

    // -------------------------------------------------------------------------
    // Branch isolation — exact-argument Received() on resolver and repo
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPassCallerBranchIdToResolverAndRepo()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var otherBranchId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, productId, rows: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();
        using var cancellation = new CancellationTokenSource();

        await useCase.Execute(request, cancellation.Token);

        await ctx.CashVarianceProductResolver.Received(1).GetIdAsync(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.DailyCloseItemsRepository.Received(1)
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(v => v == branchUser.BranchId),
                Arg.Is<Guid>(v => v == productId),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.DailyCloseItemsRepository.DidNotReceive()
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(v => v == otherBranchId),
                Arg.Any<Guid>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Account filter narrowing — exact-argument Received() with accountId
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPassAccountIdToRepo_WhenAccountIdProvided()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var rows = new List<VarianceTimeSeriesRow>
        {
            new(DateFrom, account.Id, account.Name, 75m, DailyCloseStatus.Approved)
        };

        var ctx = BuildContext(branchUser, productId, rows: rows, account: account);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(1);
        response.Items[0].AccountId.ShouldBe(account.Id);

        await ctx.DailyCloseItemsRepository.Received(1)
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(v => v == branchUser.BranchId),
                Arg.Is<Guid>(v => v == productId),
                Arg.Is<Guid?>(v => v == account.Id),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // Account filter — 404 when cross-branch account
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow404_WhenAccountIdBelongsToAnotherBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var crossBranchAccountId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, productId, rows: [], account: null);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(crossBranchAccountId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithAccountId(crossBranchAccountId)
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<NotFoundException>();
    }

    // -------------------------------------------------------------------------
    // Resolver called with exact productId passed to repo
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldPassResolvedProductIdToRepo()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, productId, rows: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        await useCase.Execute(request);

        await ctx.DailyCloseItemsRepository.Received(1)
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Is<Guid>(v => v == productId),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>());
    }

    // -------------------------------------------------------------------------
    // In-memory pagination metadata
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnCorrectPaginationMetadata_WhenMultiplePages()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var productId = Guid.NewGuid();

        var rows = Enumerable.Range(1, 7)
            .Select(i => new VarianceTimeSeriesRow(
                DateFrom.AddDays(i - 1),
                Guid.NewGuid(),
                $"Account {i}",
                i * 10m,
                DailyCloseStatus.Approved))
            .ToList();

        var ctx = BuildContext(branchUser, productId, rows: rows);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .WithPage(2)
            .WithPageSize(3)
            .Build();

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasPrevious.ShouldBeTrue();
        response.HasNext.ShouldBeTrue();
        response.Items.Count.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // Member throws 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var ctx = BuildContext(branchUser, Guid.NewGuid(), rows: []);
        var useCase = CreateUseCase(ctx);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(DateFrom)
            .WithDateTo(DateTo)
            .Build();

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static GetCashVarianceSummaryUseCase CreateUseCase(TestContext ctx)
    {
        return new GetCashVarianceSummaryUseCase(
            ctx.AuthenticationService,
            ctx.AccountsRepository,
            ctx.DailyCloseItemsRepository,
            ctx.CashVarianceProductResolver);
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        Guid productId,
        IReadOnlyList<VarianceTimeSeriesRow> rows,
        Account? account = null)
    {
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, productId)
            .Build();

        var repoBuilder = new DailyCloseItemsRepositoryBuilder()
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(
                branchUser.BranchId, productId, null, DateFrom, DateTo, rows);

        var accountsRepoBuilder = new AccountsRepositoryBuilder();
        if (account is not null)
        {
            accountsRepoBuilder.GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account);

            repoBuilder.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(
                branchUser.BranchId, productId, account.Id, DateFrom, DateTo, rows);
        }

        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            AccountsRepository = accountsRepoBuilder.Build(),
            DailyCloseItemsRepository = repoBuilder.Build(),
            CashVarianceProductResolver = resolver
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required IDailyCloseItemsRepository DailyCloseItemsRepository { get; init; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
    }
}
