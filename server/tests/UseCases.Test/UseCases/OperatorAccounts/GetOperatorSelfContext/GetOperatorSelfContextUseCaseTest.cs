using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using server.Application.UseCases.OperatorAccounts.GetOperatorSelfContext;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.OperatorAccounts.GetOperatorSelfContext;

public class GetOperatorSelfContextUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnEmptyContext_WhenNoOperatorLinkedToUser()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByUserIdAndBranchId(null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute();

        response.ShouldNotBeNull();
        response.OperatorId.ShouldBeNull();
        response.OperatorName.ShouldBeNull();
        response.PrimaryAccount.ShouldBeNull();
        response.AvailableAccounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldReturnOperatorWithNoPrimary_WhenOperatorHasNoLinks()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByUserIdAndBranchId(op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdWithAccount([])
            .Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute();

        response.OperatorId.ShouldBe(op.Id);
        response.OperatorName.ShouldBe(op.Name);
        response.PrimaryAccount.ShouldBeNull();
        response.AvailableAccounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldReturnPrimaryAndAvailableAccounts_WhenOperatorHasLinks()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();

        var primaryAccount = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var secondaryAccount = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var primaryLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(primaryAccount)
            .WithIsPrimary(true)
            .Build();
        var secondaryLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(secondaryAccount)
            .WithIsPrimary(false)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByUserIdAndBranchId(op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdWithAccount([primaryLink, secondaryLink])
            .Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute();

        response.OperatorId.ShouldBe(op.Id);
        response.OperatorName.ShouldBe(op.Name);
        response.PrimaryAccount.ShouldNotBeNull();
        response.PrimaryAccount!.AccountId.ShouldBe(primaryAccount.Id);
        response.PrimaryAccount.IsPrimary.ShouldBeTrue();
        response.AvailableAccounts.Count.ShouldBe(2);
        response.AvailableAccounts.ShouldContain(a => a.AccountId == primaryAccount.Id);
        response.AvailableAccounts.ShouldContain(a => a.AccountId == secondaryAccount.Id);
    }

    [Fact]
    public async Task Execute_ShouldReturnNullPrimary_WhenOperatorHasLinksButNoneIsPrimary()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();

        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var link = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(account)
            .WithIsPrimary(false)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByUserIdAndBranchId(op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdWithAccount([link])
            .Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute();

        response.OperatorId.ShouldBe(op.Id);
        response.PrimaryAccount.ShouldBeNull();
        response.AvailableAccounts.Count.ShouldBe(1);
    }

    private static GetOperatorSelfContextUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IOperatorAccountsRepository operatorAccountsRepo)
    {
        return new GetOperatorSelfContextUseCase(authService, operatorsRepo, operatorAccountsRepo);
    }
}
