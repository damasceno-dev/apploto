using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Transactions;

public class MemberTransactionScopeResolverTest
{
    [Fact]
    public async Task Resolve_ShouldReturnNoLinkedOperatorAndEmptyAllowedAccounts_WhenCallerHasNoActiveLinkedOperator()
    {
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId, null)
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build();
        var resolver = new MemberTransactionScopeResolver(operatorsRepository, operatorAccountsRepository);

        var scope = await resolver.Resolve(userId, branchId);

        scope.LinkedOperator.ShouldBeNull();
        scope.AllowedAccountIds.ShouldBeEmpty();
        await operatorAccountsRepository.DidNotReceive()
            .ListActiveByOperatorIdAsNoTracking(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Resolve_ShouldReturnLinkedOperatorAndActiveLinkedAccountIds()
    {
        var userId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var linkedOperator = new OperatorBuilder()
            .WithBranchId(branchId)
            .WithUserId(userId)
            .Build();
        var firstAccountId = Guid.NewGuid();
        var secondAccountId = Guid.NewGuid();
        var linkedAccounts = new[]
        {
            new OperatorAccountBuilder()
                .WithOperator(linkedOperator)
                .WithAccountId(firstAccountId)
                .Build(),
            new OperatorAccountBuilder()
                .WithOperator(linkedOperator)
                .WithAccountId(secondAccountId)
                .Build()
        };
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId, linkedOperator)
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(linkedOperator.Id, linkedAccounts)
            .Build();
        var resolver = new MemberTransactionScopeResolver(operatorsRepository, operatorAccountsRepository);

        var scope = await resolver.Resolve(userId, branchId);

        scope.LinkedOperator.ShouldBe(linkedOperator);
        scope.AllowedAccountIds.ShouldBe([firstAccountId, secondAccountId]);
        await operatorsRepository.Received(1)
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(userId, branchId);
        await operatorAccountsRepository.Received(1)
            .ListActiveByOperatorIdAsNoTracking(linkedOperator.Id);
    }
}
