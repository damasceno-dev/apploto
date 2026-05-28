using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.FiadoBalance;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.FiadoBalance;

public class GetFiadoBalancesUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2025, 5, 28, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedLocalBusinessDate = new(2025, 5, 28);

    [Fact]
    public async Task Execute_ShouldReturnBalancesEnvelope_WhenManagerCallsWithMultipleClients()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 20);
        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var rows = new List<FiadoClientBalanceRow>
        {
            new(clientA, "Alpha", 150m),
            new(clientB, "Beta", 75m)
        };
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(asOfDate)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, asOfDate, rows)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.ShouldNotBeNull();
        response.AsOfDate.ShouldBe(asOfDate);
        response.Items.Count.ShouldBe(2);
        response.Items[0].ClientId.ShouldBe(clientA);
        response.Items[0].ClientName.ShouldBe("Alpha");
        response.Items[0].OutstandingTotal.ShouldBe(150m);
        response.Items[1].ClientId.ShouldBe(clientB);
        response.Items[1].ClientName.ShouldBe("Beta");
        response.Items[1].OutstandingTotal.ShouldBe(75m);
        response.TotalOutstanding.ShouldBe(225m);
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestFiadoBalanceJsonBuilder().Build();
        var ctx = BuildContext(branchUser);
        var useCase = CreateUseCase(ctx);

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    [Fact]
    public async Task Execute_ShouldOnlyReturnRowsForCallerBranch_WhenInvokedAcrossBranches()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var otherBranchId = Guid.NewGuid();
        var asOfDate = new DateTime(2025, 5, 28);
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(asOfDate)
            .Build();
        var visibleRow = new FiadoClientBalanceRow(Guid.NewGuid(), "Visible", 50m);
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, asOfDate, [visibleRow])
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(otherBranchId, null, asOfDate, [new FiadoClientBalanceRow(Guid.NewGuid(), "Hidden", 999m)])
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        response.Items[0].ClientName.ShouldBe("Visible");
        response.TotalOutstanding.ShouldBe(50m);

        await ctx.TransactionsRepository.Received(1).ListFiadoBalancesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == asOfDate));
        await ctx.TransactionsRepository.DidNotReceive().ListFiadoBalancesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId),
            Arg.Any<Guid?>(),
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Execute_ShouldExcludeZeroBalanceClients_WhenRepositoryFiltersThem()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(asOfDate)
            .Build();
        var rows = new List<FiadoClientBalanceRow>
        {
            new(Guid.NewGuid(), "Has Balance", 200m)
        };
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, asOfDate, rows)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        response.Items.ShouldAllBe(item => item.OutstandingTotal != 0m);
    }

    [Fact]
    public async Task Execute_ShouldExcludeCancelledAndDraftFromSums_WhenRepositoryFiltersThem()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(asOfDate)
            .Build();
        var activeOnlyRow = new FiadoClientBalanceRow(Guid.NewGuid(), "ActiveOnly", 100m);
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, asOfDate, [activeOnlyRow])
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        response.Items[0].OutstandingTotal.ShouldBe(100m);
        response.TotalOutstanding.ShouldBe(100m);
    }

    [Fact]
    public async Task Execute_ShouldFallBackToBranchClockLocalBusinessDate_WhenAsOfDateOmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestFiadoBalanceJsonBuilder().Build();
        var rows = new List<FiadoClientBalanceRow>
        {
            new(Guid.NewGuid(), "Alice", 10m)
        };
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListFiadoBalancesByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, FixedLocalBusinessDate, rows)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.AsOfDate.ShouldBe(FixedLocalBusinessDate);

        await ctx.TransactionsRepository.Received(1).ListFiadoBalancesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == FixedLocalBusinessDate));
    }

    private static GetFiadoBalancesUseCase CreateUseCase(TestContext ctx)
    {
        return new GetFiadoBalancesUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.BranchClock);
    }

    private static TestContext BuildContext(BranchUser branchUser)
    {
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDate(FixedUtcNow).Returns(FixedLocalBusinessDate);

        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            TransactionsRepository = new TransactionsRepositoryBuilder().Build(),
            BranchClock = branchClock
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IBranchClock BranchClock { get; init; }
    }
}
