using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.UseCases.Transactions.List;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.List;

public class ListTransactionsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldPassResolvedFilterToListAndCount_AndUseCountForTotalCount()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var accountId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dateFrom = new DateTime(2025, 3, 1);
        var dateTo = new DateTime(2025, 3, 31);
        var request = new RequestListTransactionsJsonBuilder()
            .WithAccountId(accountId)
            .WithDateFrom(dateFrom)
            .WithDateTo(dateTo)
            .WithStatus(TransactionStatus.Active)
            .WithOperatorId(operatorId)
            .WithClientId(clientId)
            .WithPage(2)
            .WithPageSize(3)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            AccountId = accountId,
            DateFrom = dateFrom,
            DateTo = dateTo,
            Status = TransactionStatus.Active,
            OperatorId = operatorId,
            ClientId = clientId,
            Page = 2,
            PageSize = 3
        };
        var transactions = BuildTransactions(branchUser.BranchId, accountId, 3);
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, transactions)
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 42)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(3);
        response.TotalCount.ShouldBe(42);
        response.Page.ShouldBe(2);
        response.PageSize.ShouldBe(3);
        await ctx.TransactionsRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
        await ctx.TransactionsRepository.Received(1).CountByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepositoryCalls_WhenMemberHasNoLinkedOperatorAndNoExplicitAccountId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestListTransactionsJsonBuilder().Build();
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
        await ctx.TransactionsRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
        await ctx.TransactionsRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldShortCircuitWithoutRepositoryCalls_WhenMemberHasLinkedOperatorButNoActiveAccountsAndNoExplicitAccountId()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var request = new RequestListTransactionsJsonBuilder().Build();
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
        await ctx.TransactionsRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
        await ctx.TransactionsRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
    }

    [Fact]
    public async Task Execute_ShouldPopulateActionFieldsAndJoinedNames_OnListItems()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var originTransactionId = Guid.NewGuid();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Caixa Centro")
            .Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Maria")
            .Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Entradas",
            DefaultDirection = Direction.In,
            BranchId = branchUser.BranchId
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .WithName("Depósito")
            .Build();
        var transaction = new TransactionBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithAccount(account)
            .WithClient(client)
            .WithTransactionType(transactionType)
            .WithVersion(42)
            .WithOriginTransactionId(originTransactionId)
            .Build();
        var request = new RequestListTransactionsJsonBuilder().Build();
        var expectedFilter = new TransactionListFilter
        {
            Page = 1,
            PageSize = 50
        };
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [transaction])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 1)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.Items.Count.ShouldBe(1);
        var item = response.Items[0];
        item.Version.ShouldBe(42u);
        item.OriginTransactionId.ShouldBe(originTransactionId);
        item.AccountName.ShouldBe("Caixa Centro");
        item.ClientName.ShouldBe("Maria");
        item.TransactionTypeName.ShouldBe("Depósito");
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
        var request = new RequestListTransactionsJsonBuilder()
            .WithPage(page)
            .WithPageSize(pageSize)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            Page = page,
            PageSize = pageSize
        };
        var ctx = BuildContext(branchUser);
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
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
        var request = new RequestListTransactionsJsonBuilder()
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
        response.Page.ShouldBe(1);
        response.PageSize.ShouldBe(50);
        response.TotalCount.ShouldBe(0);
        await ctx.TransactionsRepository.DidNotReceive()
            .ListByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
        await ctx.TransactionsRepository.DidNotReceive()
            .CountByBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<TransactionListFilter>());
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
        var request = new RequestListTransactionsJsonBuilder()
            .WithPage(1)
            .WithPageSize(25)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            AllowedAccountIds = linkedAccountIds,
            Page = 1,
            PageSize = 25
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
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(
                branchUser.BranchId,
                expectedFilter,
                BuildTransactions(branchUser.BranchId, linkedAccountIds[0], 1))
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 1)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(1);
        await ctx.TransactionsRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
        await ctx.TransactionsRepository.Received(1).CountByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
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
        var request = new RequestListTransactionsJsonBuilder()
            .WithOperatorId(ignoredOperatorId)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            AllowedAccountIds = [linkedAccountId],
            Page = 1,
            PageSize = 50
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
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.TransactionsRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
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
        var request = new RequestListTransactionsJsonBuilder()
            .WithMine(true)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            OperatorId = callerOperator.Id,
            AllowedAccountIds = [linkedAccountId],
            Page = 1,
            PageSize = 50
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
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.TransactionsRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    [Fact]
    public async Task Execute_ShouldSetOperatorIdToCallerOperator_WhenManagerUsesMine()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var request = new RequestListTransactionsJsonBuilder()
            .WithMine(true)
            .Build();
        var expectedFilter = new TransactionListFilter
        {
            OperatorId = callerOperator.Id,
            Page = 1,
            PageSize = 50
        };
        var ctx = BuildContext(branchUser);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [])
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .ListByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, [])
            .CountByBranchIdAsNoTrackingReturns(branchUser.BranchId, expectedFilter, 0)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(request);

        response.TotalCount.ShouldBe(0);
        await ctx.TransactionsRepository.Received(1).ListByBranchIdAsNoTracking(
            branchUser.BranchId,
            Arg.Is<TransactionListFilter>(actual => MatchesFilter(expectedFilter, actual)));
    }

    private static ListTransactionsUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);

        return new ListTransactionsUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            memberAccountScopeResolver);
    }

    private static TestContext BuildContext(BranchUser branchUser)
    {
        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            TransactionsRepository = new TransactionsRepositoryBuilder().Build(),
            OperatorsRepository = new OperatorsRepositoryBuilder().Build(),
            OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build()
        };
    }

    private static IReadOnlyList<Transaction> BuildTransactions(Guid branchId, Guid accountId, int count)
    {
        return Enumerable.Range(1, count)
            .Select(_ => new TransactionBuilder()
                .WithBranchId(branchId)
                .WithAccountId(accountId)
                .Build())
            .ToList();
    }

    private static bool MatchesFilter(TransactionListFilter expected, TransactionListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.Status == actual.Status &&
               expected.OperatorId == actual.OperatorId &&
               expected.ClientId == actual.ClientId &&
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
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
