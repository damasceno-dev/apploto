using CommonTestUtilities.Entities;
using CommonTestUtilities.Filters;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Reports.DailyLedger;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Reports.DailyLedger;

public class GetDailyLedgerUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnLedgerEnvelope_WhenManagerCallsWithValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = new DateTime(2025, 1, 31);
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithPage(1)
            .WithPageSize(50)
            .Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Entradas",
            DefaultDirection = Direction.In,
            BranchId = branchUser.BranchId
        };
        var transactionType = new TransactionTypeBuilder().WithCategory(category).Build();
        var recordedByOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var transaction = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithTransactionType(transactionType)
            .WithRecordedByOperator(recordedByOperator)
            .WithDate(dateFrom)
            .Build();
        var filter = new DailyLedgerListFilterBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithPage(1)
            .WithPageSize(50)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, [transaction])
            .CountByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, 1)
            .SumActiveByAccountAndDateBeforeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, 100m)
            .SumActiveByAccountAndDateRangeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, dateTo, 200m, 50m)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.ShouldNotBeNull();
        response.Items.Count.ShouldBe(1);
        response.TotalCount.ShouldBe(1);
        response.OpeningBalance.ShouldBe(100m);
        response.TotalIn.ShouldBe(200m);
        response.TotalOut.ShouldBe(50m);
        response.NetTotal.ShouldBe(150m);
        response.ClosingBalance.ShouldBe(250m);
        response.AccountId.ShouldBe(account.Id);
        response.AccountName.ShouldBe(account.Name);
    }

    [Fact]
    public async Task Execute_ShouldThrow403_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestDailyLedgerJsonBuilder().Build();
        var ctx = BuildContext(branchUser);
        var useCase = CreateUseCase(ctx);

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<TokenWithoutPermissionException>();
    }

    [Fact]
    public async Task Execute_ShouldThrow404_WhenAccountDoesNotBelongToBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(Guid.NewGuid())
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, branchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var action = async () => await useCase.Execute(request);

        await action.ShouldThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData(25, 10, 1, 3, true, false)]
    [InlineData(25, 10, 2, 3, true, true)]
    [InlineData(25, 10, 3, 3, false, true)]
    [InlineData(0, 10, 1, 0, false, false)]
    [InlineData(10, 10, 1, 1, false, false)]
    public async Task Execute_ShouldComputePaginationMetadata(
        int totalCount,
        int pageSize,
        int page,
        int expectedTotalPages,
        bool expectedHasNext,
        bool expectedHasPrevious)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = new DateTime(2025, 1, 31);
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithPage(page)
            .WithPageSize(pageSize)
            .Build();
        var filter = new DailyLedgerListFilterBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithPage(page)
            .WithPageSize(pageSize)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, [])
            .CountByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, totalCount)
            .SumActiveByAccountAndDateBeforeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, 0m)
            .SumActiveByAccountAndDateRangeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, dateTo, 0m, 0m)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(totalCount);
        response.TotalPages.ShouldBe(expectedTotalPages);
        response.HasNext.ShouldBe(expectedHasNext);
        response.HasPrevious.ShouldBe(expectedHasPrevious);
    }

    [Fact]
    public async Task Execute_ShouldReturnZeroTotals_WhenWindowIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var dateFrom = new DateTime(2025, 1, 1);
        var dateTo = new DateTime(2025, 1, 31);
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .Build();
        var filter = new DailyLedgerListFilterBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, [])
            .CountByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, 0)
            .SumActiveByAccountAndDateBeforeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, 0m)
            .SumActiveByAccountAndDateRangeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, dateTo, 0m, 0m)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.ShouldBeEmpty();
        response.TotalIn.ShouldBe(0m);
        response.TotalOut.ShouldBe(0m);
        response.NetTotal.ShouldBe(0m);
        response.OpeningBalance.ShouldBe(0m);
        response.ClosingBalance.ShouldBe(0m);
    }

    [Fact]
    public async Task Execute_ShouldPassExactArgumentsToOpeningBalanceAndWindowTotals()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var dateFrom = new DateTime(2025, 3, 1);
        var dateTo = new DateTime(2025, 3, 31);
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .Build();
        var filter = new DailyLedgerListFilterBuilder()
            .WithAccountId(account.Id)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .Build();
        var ctx = BuildContext(branchUser);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, [])
            .CountByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(branchUser.BranchId, filter, 0)
            .SumActiveByAccountAndDateBeforeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, 0m)
            .SumActiveByAccountAndDateRangeAsNoTrackingReturns(branchUser.BranchId, account.Id, dateFrom, dateTo, 0m, 0m)
            .Build();
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(request);

        await ctx.TransactionsRepository.Received(1).SumActiveByAccountAndDateBeforeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == account.Id),
            Arg.Is<DateTime>(v => v == dateFrom));

        await ctx.TransactionsRepository.Received(1).SumActiveByAccountAndDateRangeAsNoTracking(
            Arg.Is<Guid>(v => v == branchUser.BranchId),
            Arg.Is<Guid>(v => v == account.Id),
            Arg.Is<DateTime>(v => v == dateFrom),
            Arg.Is<DateTime>(v => v == dateTo));
    }

    private static GetDailyLedgerUseCase CreateUseCase(TestContext ctx)
    {
        return new GetDailyLedgerUseCase(
            ctx.AuthenticationService,
            ctx.AccountsRepository,
            ctx.TransactionsRepository);
    }

    private static TestContext BuildContext(BranchUser branchUser)
    {
        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            AccountsRepository = new AccountsRepositoryBuilder().Build(),
            TransactionsRepository = new TransactionsRepositoryBuilder().Build()
        };
    }

    private class TestContext
    {
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
    }
}
