using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.Dashboard;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.Dashboard;

public class GetDashboardUseCaseTest
{
    private static readonly DateTime Date = new(2025, 5, 20);
    private static readonly DateTime FixedUtcNow = new(2025, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedLocalBusinessDate = new(2025, 6, 1);

    [Fact]
    public async Task Execute_ShouldProjectClosesPendingCountAndNotSubmitted_WhenDayIsMixed()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var submitted = BuildCloseRow("Terminal A", DailyCloseStatus.Submitted, submittedAt: FixedUtcNow);
        var approved = BuildCloseRow("Terminal B", DailyCloseStatus.Approved, submittedAt: FixedUtcNow, approvedAt: FixedUtcNow);
        var rejected = BuildCloseRow("Terminal C", DailyCloseStatus.Rejected, submittedAt: FixedUtcNow);
        var missing = new ExpectedCloserRow(Guid.NewGuid(), "Terminal D", Guid.NewGuid(), "Operator D");
        var varianceRows = new List<VarianceTimeSeriesRow>
        {
            new(Date, submitted.AccountId, submitted.AccountName, -30m, DailyCloseStatus.Submitted),
            new(Date, approved.AccountId, approved.AccountName, 150m, DailyCloseStatus.Approved),
            new(Date, rejected.AccountId, rejected.AccountName, 20m, DailyCloseStatus.Rejected)
        };
        var expected = new List<ExpectedCloserRow>
        {
            ToExpectedCloser(submitted), ToExpectedCloser(approved), ToExpectedCloser(rejected), missing
        };

        var ctx = BuildContext(branchUser, closes: [submitted, approved, rejected], varianceRows: varianceRows, expectedClosers: expected);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Date.ShouldBe(Date);
        response.Closes.Count.ShouldBe(3);

        var submittedRow = response.Closes.Single(c => c.AccountId == submitted.AccountId);
        submittedRow.DailyCloseId.ShouldBe(submitted.DailyCloseId);
        submittedRow.AccountName.ShouldBe("Terminal A");
        submittedRow.RecordedByOperatorId.ShouldBe(submitted.RecordedByOperatorId);
        submittedRow.RecordedByOperatorName.ShouldBe(submitted.RecordedByOperatorName);
        submittedRow.SubmittedByOperatorId.ShouldBe(submitted.SubmittedByOperatorId);
        submittedRow.SubmittedByOperatorName.ShouldBe(submitted.SubmittedByOperatorName);
        submittedRow.Status.ShouldBe(DailyCloseStatus.Submitted);
        submittedRow.SubmittedAt.ShouldBe(FixedUtcNow);
        submittedRow.VarianceValue.ShouldBe(-30m);

        response.Closes.Single(c => c.AccountId == approved.AccountId).VarianceValue.ShouldBe(150m);
        response.Closes.Single(c => c.AccountId == approved.AccountId).ApprovedAt.ShouldBe(FixedUtcNow);
        response.Closes.Single(c => c.AccountId == rejected.AccountId).VarianceValue.ShouldBe(20m);

        response.PendingApprovalCount.ShouldBe(1);

        var notSubmitted = response.NotSubmitted.ShouldHaveSingleItem();
        notSubmitted.AccountId.ShouldBe(missing.AccountId);
        notSubmitted.AccountName.ShouldBe("Terminal D");
        notSubmitted.OperatorId.ShouldBe(missing.OperatorId);
        notSubmitted.OperatorName.ShouldBe("Operator D");
        notSubmitted.DailyCloseId.ShouldBeNull();
        notSubmitted.Status.ShouldBeNull();

        response.TotalVariance.ShouldBe(140m);
        response.MeanVariance.ShouldBe(140m / 3m);
    }

    [Fact]
    public async Task Execute_ShouldCountOnlySubmittedClosesAsPendingApproval()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var closes = new List<DashboardCloseRow>
        {
            BuildCloseRow("Terminal A", DailyCloseStatus.Submitted),
            BuildCloseRow("Terminal B", DailyCloseStatus.Submitted),
            BuildCloseRow("Terminal C", DailyCloseStatus.Approved),
            BuildCloseRow("Terminal D", DailyCloseStatus.Rejected)
        };

        var ctx = BuildContext(branchUser, closes: closes, varianceRows: [], expectedClosers: []);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.PendingApprovalCount.ShouldBe(2);
        response.Closes.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Execute_ShouldTreatDraftCloseAsNotSubmitted_AndCarryItsCloseIdForDeepLink()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var draft = BuildCloseRow("Terminal Draft", DailyCloseStatus.Draft);
        // Recalled Submitted -> Draft closes keep their system-managed variance row (§6.5).
        var varianceRows = new List<VarianceTimeSeriesRow>
        {
            new(Date, draft.AccountId, draft.AccountName, 12m, DailyCloseStatus.Draft)
        };

        var ctx = BuildContext(
            branchUser, closes: [draft], varianceRows: varianceRows, expectedClosers: [ToExpectedCloser(draft)]);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Closes.ShouldBeEmpty();
        response.PendingApprovalCount.ShouldBe(0);
        var notSubmitted = response.NotSubmitted.ShouldHaveSingleItem();
        notSubmitted.AccountId.ShouldBe(draft.AccountId);
        notSubmitted.DailyCloseId.ShouldBe(draft.DailyCloseId);
        notSubmitted.Status.ShouldBe(DailyCloseStatus.Draft);
        response.TotalVariance.ShouldBe(12m);
    }

    [Fact]
    public async Task Execute_ShouldReturnNullVariance_WhenCloseHasNoSubmittedVarianceRow()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildCloseRow("Terminal A", DailyCloseStatus.Submitted);

        var ctx = BuildContext(branchUser, closes: [close], varianceRows: [], expectedClosers: []);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Closes.ShouldHaveSingleItem().VarianceValue.ShouldBeNull();
        response.TotalVariance.ShouldBe(0m);
        response.MeanVariance.ShouldBe(0m);
    }

    [Fact]
    public async Task Execute_ShouldJoinVarianceByDateAndAccount_WhenSiblingAccountsShareTheDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var productId = Guid.NewGuid();
        var closeA = BuildCloseRow("Terminal A", DailyCloseStatus.Approved);
        var closeB = BuildCloseRow("Terminal B", DailyCloseStatus.Approved);
        var varianceRows = new List<VarianceTimeSeriesRow>
        {
            new(Date, closeA.AccountId, closeA.AccountName, 10m, DailyCloseStatus.Approved),
            new(Date, closeB.AccountId, closeB.AccountName, -25m, DailyCloseStatus.Approved)
        };

        var ctx = BuildContext(
            branchUser, closes: [closeA, closeB], varianceRows: varianceRows, expectedClosers: [], productId: productId);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Closes.Single(c => c.AccountId == closeA.AccountId).VarianceValue.ShouldBe(10m);
        response.Closes.Single(c => c.AccountId == closeB.AccountId).VarianceValue.ShouldBe(-25m);
        response.TotalVariance.ShouldBe(-15m);

        await ctx.DailyCloseItemsRepository.Received(1).ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == productId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == Date),
            Arg.Is<DateTime>(v => v == Date));
    }

    [Fact]
    public async Task Execute_ShouldReturnZeroAggregatesAndAllExpectedAsNotSubmitted_WhenDayIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var expected = new List<ExpectedCloserRow>
        {
            new(Guid.NewGuid(), "Terminal A", Guid.NewGuid(), "Operator A"),
            new(Guid.NewGuid(), "Terminal B", Guid.NewGuid(), "Operator B")
        };

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], expectedClosers: expected);
        var response = await CreateUseCase(ctx).Execute(BuildRequest());

        response.Closes.ShouldBeEmpty();
        response.PendingApprovalCount.ShouldBe(0);
        response.TotalVariance.ShouldBe(0m);
        response.MeanVariance.ShouldBe(0m);
        response.NotSubmitted.Count.ShouldBe(2);
        response.NotSubmitted.Select(n => n.AccountName).ShouldBe(["Terminal A", "Terminal B"]);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyNotSubmitted_WhenDateIsAfterBranchLocalToday()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var futureDate = FixedLocalBusinessDate.AddDays(1);
        var expected = new List<ExpectedCloserRow>
        {
            new(Guid.NewGuid(), "Terminal A", Guid.NewGuid(), "Operator A")
        };

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], expectedClosers: expected, date: futureDate);
        var response = await CreateUseCase(ctx).Execute(BuildRequest(futureDate));

        response.NotSubmitted.ShouldBeEmpty();
        response.Closes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldPassCallerBranchToRepositories()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var otherBranchId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], expectedClosers: [], productId: productId);
        using var cancellation = new CancellationTokenSource();
        await CreateUseCase(ctx).Execute(BuildRequest(), cancellation.Token);

        await ctx.DailyClosesRepository.Received(1).ListDashboardClosesByBranchIdAndDateAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<DateTime>(v => v == Date),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));
        await ctx.AccountsRepository.Received(1).ListExpectedClosersByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));
        await ctx.CashVarianceProductResolver.Received(1).GetIdAsync(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<CancellationToken>(value => value == cancellation.Token));

        await ctx.DailyClosesRepository.DidNotReceive().ListDashboardClosesByBranchIdAndDateAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        await ctx.AccountsRepository.DidNotReceive().ListExpectedClosersByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], expectedClosers: []);

        var action = async () => await CreateUseCase(ctx).Execute(BuildRequest());

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidationException_WhenDateIsDefault()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var ctx = BuildContext(branchUser, closes: [], varianceRows: [], expectedClosers: []);

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            CreateUseCase(ctx).Execute(BuildRequest(default(DateTime))));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
        await ctx.CashVarianceProductResolver.DidNotReceive().GetIdAsync(Arg.Any<Guid>());
        await ctx.DailyClosesRepository.DidNotReceive().ListDashboardClosesByBranchIdAndDateAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<DateTime>());
        await ctx.DailyCloseItemsRepository.DidNotReceive()
            .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<DateTime>(), Arg.Any<DateTime>());
        await ctx.AccountsRepository.DidNotReceive().ListExpectedClosersByBranchIdAsNoTracking(Arg.Any<Guid>());
    }

    private static RequestDashboardJson BuildRequest(DateTime? date = null)
    {
        return new RequestDashboardJsonBuilder()
            .WithDate(date ?? Date)
            .Build();
    }

    private static DashboardCloseRow BuildCloseRow(
        string accountName,
        DailyCloseStatus status,
        DateTime? submittedAt = null,
        DateTime? approvedAt = null)
    {
        var hasIdentity = status != DailyCloseStatus.Draft;
        return new DashboardCloseRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            accountName,
            hasIdentity ? Guid.NewGuid() : null,
            hasIdentity ? $"Recorder user {accountName}" : null,
            hasIdentity ? Guid.NewGuid() : null,
            hasIdentity ? $"Operator {accountName}" : null,
            hasIdentity ? Guid.NewGuid() : null,
            hasIdentity ? $"Submitter user {accountName}" : null,
            hasIdentity ? Guid.NewGuid() : null,
            hasIdentity ? $"Submitter operator {accountName}" : null,
            status,
            submittedAt,
            approvedAt);
    }

    private static ExpectedCloserRow ToExpectedCloser(DashboardCloseRow close)
    {
        // Draft close rows carry no submitting operator, but the account is still expected to
        // close through some assigned operator — fabricate one for the expected-closer side.
        return new ExpectedCloserRow(
            close.AccountId,
            close.AccountName,
            close.RecordedByOperatorId ?? Guid.NewGuid(),
            close.RecordedByOperatorName ?? $"Operator {close.AccountName}");
    }

    private static GetDashboardUseCase CreateUseCase(TestContext ctx)
    {
        return new GetDashboardUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.DailyCloseItemsRepository,
            ctx.AccountsRepository,
            ctx.CashVarianceProductResolver,
            ctx.BranchClock);
    }

    private static TestContext BuildContext(
        BranchUser branchUser,
        IReadOnlyList<DashboardCloseRow> closes,
        IReadOnlyList<VarianceTimeSeriesRow> varianceRows,
        IReadOnlyList<ExpectedCloserRow> expectedClosers,
        Guid? productId = null,
        DateTime? date = null)
    {
        var resolvedProductId = productId ?? Guid.NewGuid();
        var resolvedDate = date ?? Date;

        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDate(FixedUtcNow).Returns(FixedLocalBusinessDate);

        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            DailyClosesRepository = new DailyClosesRepositoryBuilder()
                .ListDashboardClosesByBranchIdAndDateAsNoTrackingReturns(branchUser.BranchId, resolvedDate, closes)
                .Build(),
            DailyCloseItemsRepository = new DailyCloseItemsRepositoryBuilder()
                .ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTrackingReturns(
                    branchUser.BranchId, resolvedProductId, null, resolvedDate, resolvedDate, varianceRows)
                .Build(),
            AccountsRepository = new AccountsRepositoryBuilder()
                .ListExpectedClosersByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedClosers)
                .Build(),
            CashVarianceProductResolver = new CashVarianceProductResolverBuilder()
                .ReturnsId(branchUser.BranchId, resolvedProductId)
                .Build(),
            BranchClock = branchClock
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required IDailyCloseItemsRepository DailyCloseItemsRepository { get; init; }
        public required IAccountsRepository AccountsRepository { get; init; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
        public required IBranchClock BranchClock { get; init; }
    }
}
