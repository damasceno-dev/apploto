using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using server.Application.UseCases.OperatorAccounts.ListOperatorAccounts;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.OperatorAccounts.ListOperatorAccounts;

public class ListOperatorAccountsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnActiveLinks_WithAccountContext()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account1 = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var account2 = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var link1 = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(account1)
            .WithIsPrimary(true)
            .Build();
        var link2 = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(account2)
            .WithIsPrimary(false)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdWithAccount([link1, link2])
            .Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute(op.Id);

        response.OperatorAccounts.ShouldNotBeNull();
        response.OperatorAccounts.Count.ShouldBe(2);
        response.OperatorAccounts.ShouldContain(a => a.AccountId == account1.Id && a.IsPrimary);
        response.OperatorAccounts.ShouldContain(a => a.AccountId == account2.Id && !a.IsPrimary);
        response.OperatorAccounts.All(_ => true).ShouldBeTrue();
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenNoLinksExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdWithAccount([])
            .Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var response = await useCase.Execute(op.Id);

        response.OperatorAccounts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, operatorAccountsRepo);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
    }

    private static ListOperatorAccountsUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IOperatorAccountsRepository operatorAccountsRepo)
    {
        return new ListOperatorAccountsUseCase(authService, operatorsRepo, operatorAccountsRepo);
    }
}
