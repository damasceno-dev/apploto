using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
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
        ctx.TransactionsRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<TransactionListFilter>())
            .Returns(transactions);
        ctx.TransactionsRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<TransactionListFilter>())
            .Returns(42);
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
        ctx.TransactionsRepository.ListByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<TransactionListFilter>())
            .Returns(BuildTransactions(branchUser.BranchId, linkedAccountIds[0], 1));
        ctx.TransactionsRepository.CountByBranchIdAsNoTracking(branchUser.BranchId, Arg.Any<TransactionListFilter>())
            .Returns(1);
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

    private static ListTransactionsUseCase CreateUseCase(TestContext ctx)
    {
        var memberTransactionScopeResolver = new MemberTransactionScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);

        return new ListTransactionsUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            memberTransactionScopeResolver);
    }

    private static TestContext BuildContext(BranchUser branchUser)
    {
        return new TestContext
        {
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            TransactionsRepository = Substitute.For<ITransactionsRepository>(),
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
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
    }
}
