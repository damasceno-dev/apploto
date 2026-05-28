using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.OpenChequeAging;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.OpenChequeAging;

public class GetOpenChequeAgingUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2025, 5, 28, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedLocalBusinessDate = new(2025, 5, 28);

    // -------------------------------------------------------------------------
    // Single-installment group (origin points to itself)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnOneGroup_WhenSingleInstallmentPlanExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();

        var group = BuildGroup(originId, outstandingTotal: 100m, oldestOpenDueDate: asOfDate.AddDays(-10),
            openRowCount: 1, totalRowCount: 1);

        var sibling = new TransactionBuilder()
            .WithId(originId)
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(asOfDate.AddDays(-10))
            .WithValue(100m)
            .WithPaidAt(null)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, [sibling])
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(1);
        response.Items.Count.ShouldBe(1);
        response.Items[0].OriginTransactionId.ShouldBe(originId);
        response.Items[0].OpenRowCount.ShouldBe(1);
        response.Items[0].TotalRowCount.ShouldBe(1);
        response.Items[0].OutstandingTotal.ShouldBe(100m);
        response.Items[0].Rows.Count.ShouldBe(1);
        response.Items[0].Rows[0].TransactionId.ShouldBe(originId);
    }

    // -------------------------------------------------------------------------
    // Multi-row group (3 rows, all unpaid)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnGroupWithThreeRows_WhenAllInstallmentsUnpaid()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();

        var due1 = asOfDate.AddDays(-30);
        var due2 = asOfDate.AddDays(-60);
        var due3 = asOfDate.AddDays(-90);

        var group = BuildGroup(originId, outstandingTotal: 300m, oldestOpenDueDate: due1,
            openRowCount: 3, totalRowCount: 3);

        var siblings = new List<Transaction>
        {
            new TransactionBuilder().WithBranchId(branchUser.BranchId).WithDueDate(due1).WithValue(100m).WithPaidAt(null).Build(),
            new TransactionBuilder().WithBranchId(branchUser.BranchId).WithDueDate(due2).WithValue(100m).WithPaidAt(null).Build(),
            new TransactionBuilder().WithBranchId(branchUser.BranchId).WithDueDate(due3).WithValue(100m).WithPaidAt(null).Build()
        };

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, siblings)
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].Rows.Count.ShouldBe(3);
        response.Items[0].OpenRowCount.ShouldBe(3);
        response.Items[0].TotalRowCount.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // Group with one paid + one unpaid
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldExcludePaidRowsFromResponseRows_WhenOneRowIsPaid()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();

        // Repo projection already accounts for only 1 unpaid row; total = 2
        var group = BuildGroup(originId, outstandingTotal: 100m, oldestOpenDueDate: asOfDate.AddDays(-10),
            openRowCount: 1, totalRowCount: 2);

        var paidSibling = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(asOfDate.AddDays(-20))
            .WithValue(100m)
            .WithPaidAt(asOfDate.AddDays(-15))
            .Build();

        var unpaidSibling = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(asOfDate.AddDays(-10))
            .WithValue(100m)
            .WithPaidAt(null)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, [paidSibling, unpaidSibling])
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].Rows.Count.ShouldBe(1);
        response.Items[0].OpenRowCount.ShouldBe(1);
        response.Items[0].TotalRowCount.ShouldBe(2);
        response.Items[0].OutstandingTotal.ShouldBe(100m);
        response.Items[0].Rows[0].TransactionId.ShouldBe(unpaidSibling.Id);
    }

    // -------------------------------------------------------------------------
    // Fully-paid group excluded (defensive — repo returns empty)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnEmptyItems_WhenNoOpenGroups()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 0)
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Branch isolation — exact-argument Received / DidNotReceive
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldQueryOnlyCallerBranch_WhenAnotherBranchExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var otherBranchId = Guid.NewGuid();
        var asOfDate = new DateTime(2025, 5, 28);

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 0)
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(request);

        await ctx.TransactionsRepository.Received(1).ListOpenChequeGroupsByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>());

        await ctx.TransactionsRepository.DidNotReceive().ListOpenChequeGroupsByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId),
            Arg.Any<Guid?>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>());
    }

    // -------------------------------------------------------------------------
    // Group-level pagination metadata
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldReturnCorrectPaginationMetadata_WhenMultiplePagesExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();

        var group = BuildGroup(originId, outstandingTotal: 50m, oldestOpenDueDate: asOfDate.AddDays(-5),
            openRowCount: 1, totalRowCount: 1);
        var sibling = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(asOfDate.AddDays(-5))
            .WithValue(50m)
            .WithPaidAt(null)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 2, 3, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 7)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, [sibling])
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(2)
            .WithPageSize(3)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasPrevious.ShouldBeTrue();
        response.HasNext.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Future-due-only group → OldestOpenBucket = Current
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldAssignCurrentBucket_WhenOldestDueDateIsInFuture()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();
        var futureDue = asOfDate.AddDays(7);

        var group = BuildGroup(originId, outstandingTotal: 150m, oldestOpenDueDate: futureDue,
            openRowCount: 1, totalRowCount: 1);

        var sibling = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(futureDue)
            .WithValue(150m)
            .WithPaidAt(null)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, [sibling])
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].OldestOpenBucket.ShouldBe(AgingBucket.Current);
        response.Items[0].Rows[0].Bucket.ShouldBe(AgingBucket.Current);
        response.Items[0].Rows[0].DaysOutstanding.ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // Very-old group → Days91Plus
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldAssignDays91PlusBucket_WhenOldestDueDateIsOver91DaysAgo()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var originId = Guid.NewGuid();
        var due91 = asOfDate.AddDays(-91);

        var group = BuildGroup(originId, outstandingTotal: 200m, oldestOpenDueDate: due91,
            openRowCount: 1, totalRowCount: 1);

        var sibling = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithDueDate(due91)
            .WithValue(200m)
            .WithPaidAt(null)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [group])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1)
            .ListActiveByOriginTransactionIdAndBranchIdAsNoTrackingReturns(originId, branchUser.BranchId, [sibling])
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].OldestOpenBucket.ShouldBe(AgingBucket.Days91Plus);
        response.Items[0].Rows[0].DaysOutstanding.ShouldBe(91);
    }

    // -------------------------------------------------------------------------
    // Member throws 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestOpenChequeAgingJsonBuilder().WithPage(1).WithPageSize(50).Build();
        var ctx = BuildContext(branchUser);
        var useCase = CreateUseCase(ctx);

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    // -------------------------------------------------------------------------
    // Clock fallback
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Execute_ShouldFallBackToBranchClockLocalBusinessDate_WhenAsOfDateOmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 1, 50, [])
            .CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(branchUser.BranchId, null, null, 0)
            .Build();

        var request = new RequestOpenChequeAgingJsonBuilder()
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.AsOfDate.ShouldBe(FixedLocalBusinessDate);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static OpenChequeGroupRow BuildGroup(
        Guid originId,
        decimal outstandingTotal,
        DateTime oldestOpenDueDate,
        int openRowCount,
        int totalRowCount)
    {
        return new OpenChequeGroupRow(
            originId,
            outstandingTotal,
            oldestOpenDueDate,
            openRowCount,
            totalRowCount,
            Guid.NewGuid(),
            "Account",
            Guid.NewGuid(),
            "Client",
            "Description");
    }

    private static GetOpenChequeAgingUseCase CreateUseCase(TestContext ctx)
    {
        return new GetOpenChequeAgingUseCase(
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
