using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Reports.FiadoAging;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.FiadoAging;

public class GetFiadoAgingUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2025, 5, 28, 15, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedLocalBusinessDate = new(2025, 5, 28);

    [Fact]
    public async Task Execute_ShouldReturnMultipleItemsWithCorrectBuckets_WhenManagerRequests()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);

        // day 30 => Days0To30, day 31 => Days31To60, future => Current
        var dueDay30 = asOfDate.AddDays(-30);
        var dueDay31 = asOfDate.AddDays(-31);
        var dueFuture = asOfDate.AddDays(7);

        var rows = BuildRows([
            (Guid.NewGuid(), dueDay30, 100m),
            (Guid.NewGuid(), dueDay31, 200m),
            (Guid.NewGuid(), dueFuture, 50m)
        ]);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, rows)
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 3)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.ShouldNotBeNull();
        response.AsOfDate.ShouldBe(asOfDate);
        response.TotalCount.ShouldBe(3);
        response.Items.Count.ShouldBe(3);

        response.Items[0].DaysOutstanding.ShouldBe(30);
        response.Items[0].Bucket.ShouldBe(AgingBucket.Days0To30);

        response.Items[1].DaysOutstanding.ShouldBe(31);
        response.Items[1].Bucket.ShouldBe(AgingBucket.Days31To60);

        response.Items[2].DaysOutstanding.ShouldBe(0);
        response.Items[2].Bucket.ShouldBe(AgingBucket.Current);
    }

    [Fact]
    public async Task Execute_ShouldAssignDays61To90Bucket_WhenDueDateIs90DaysAgo()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var due90 = asOfDate.AddDays(-90);
        var due91 = asOfDate.AddDays(-91);

        var rows = BuildRows([(Guid.NewGuid(), due90, 100m), (Guid.NewGuid(), due91, 200m)]);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, rows)
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 2)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].Bucket.ShouldBe(AgingBucket.Days61To90);
        response.Items[0].DaysOutstanding.ShouldBe(90);
        response.Items[1].Bucket.ShouldBe(AgingBucket.Days91Plus);
        response.Items[1].DaysOutstanding.ShouldBe(91);
    }

    [Fact]
    public async Task Execute_ShouldAssignCurrentBucket_WhenDueDateIsInFuture()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);
        var futureDue = asOfDate.AddDays(7);

        var rows = BuildRows([(Guid.NewGuid(), futureDue, 75m)]);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, rows)
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 1)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items[0].Bucket.ShouldBe(AgingBucket.Current);
        response.Items[0].DaysOutstanding.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_ShouldPassTabAccountTypeToRepository_WhenCalled()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, [])
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 0)
            .Build();

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(request);

        await ctx.TransactionsRepository.Received(1).ListOpenReceivablesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == asOfDate),
            Arg.Is<int>(v => v == 1),
            Arg.Is<int>(v => v == 50),
            Arg.Is<AccountType?>(v => v == AccountType.Tab));

        await ctx.TransactionsRepository.Received(1).CountOpenReceivablesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<Guid?>(v => v == null),
            Arg.Is<DateTime>(v => v == asOfDate),
            Arg.Is<AccountType?>(v => v == AccountType.Tab));
    }

    [Fact]
    public async Task Execute_ShouldOnlyQueryCallerBranch_WhenAnotherBranchExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var otherBranchId = Guid.NewGuid();
        var asOfDate = new DateTime(2025, 5, 28);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, [])
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 0)
            .Build();

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(request);

        await ctx.TransactionsRepository.DidNotReceive().ListOpenReceivablesByBranchIdAsNoTracking(
            Arg.Is<Guid>(v => v == otherBranchId),
            Arg.Any<Guid?>(),
            Arg.Any<Guid?>(),
            Arg.Any<DateTime>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<AccountType?>());
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestFiadoAgingJsonBuilder().WithPage(1).WithPageSize(50).Build();
        var ctx = BuildContext(branchUser);
        var useCase = CreateUseCase(ctx);

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    [Fact]
    public async Task Execute_ShouldReturnCorrectPaginationMetadata_WhenMultiplePagesExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var asOfDate = new DateTime(2025, 5, 28);

        var rows = BuildRows([(Guid.NewGuid(), asOfDate.AddDays(-1), 100m)]);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(2)
            .WithPageSize(3)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 2, 3, AccountType.Tab, rows)
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 7)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(7);
        response.TotalPages.ShouldBe(3);
        response.HasPrevious.ShouldBeTrue();
        response.HasNext.ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldFallBackToBranchClockLocalBusinessDate_WhenAsOfDateOmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();

        var request = new RequestFiadoAgingJsonBuilder()
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, FixedLocalBusinessDate, 1, 50, AccountType.Tab, [])
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, FixedLocalBusinessDate, AccountType.Tab, 0)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.AsOfDate.ShouldBe(FixedLocalBusinessDate);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyItems_WhenRepositoryReturnsEmptyList()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var asOfDate = new DateTime(2025, 5, 28);

        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(asOfDate)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, 1, 50, AccountType.Tab, [])
            .CountOpenReceivablesByBranchIdAsNoTrackingReturns(
                branchUser.BranchId, null, null, asOfDate, AccountType.Tab, 0)
            .Build();

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalCount.ShouldBe(0);
        response.TotalPages.ShouldBe(0);
        response.HasNext.ShouldBeFalse();
        response.HasPrevious.ShouldBeFalse();
    }

    private static IReadOnlyList<TransactionOpenReceivableRow> BuildRows(
        IEnumerable<(Guid id, DateTime dueDate, decimal value)> specs)
    {
        return specs.Select(s => new TransactionOpenReceivableRow(
            s.id,
            new DateTime(2025, 1, 1),
            s.dueDate,
            s.value,
            Guid.NewGuid(),
            "Client",
            Guid.NewGuid(),
            "Account",
            null,
            null)).ToList();
    }

    private static GetFiadoAgingUseCase CreateUseCase(TestContext ctx)
    {
        return new GetFiadoAgingUseCase(
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
