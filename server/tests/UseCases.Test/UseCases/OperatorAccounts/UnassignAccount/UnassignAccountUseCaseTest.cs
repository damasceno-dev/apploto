using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.OperatorAccounts.UnassignAccount;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.OperatorAccounts.UnassignAccount;

public class UnassignAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateLink_WhenLinkIsActive()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var activeLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(account.Id)
            .WithIsPrimary(false)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(activeLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, account.Id);

        activeLink.Active.ShouldBeFalse();
        activeLink.IsPrimary.ShouldBeFalse();
        response.AccountId.ShouldBe(account.Id);
        response.OperatorId.ShouldBe(op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearIsPrimary_WhenUnassigningPrimaryLink()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var primaryLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(account.Id)
            .WithIsPrimary(true)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(primaryLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(op.Id, account.Id);

        primaryLink.Active.ShouldBeFalse();
        primaryLink.IsPrimary.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await operatorAccountsRepo.DidNotReceive().GetByOperatorIdAndAccountId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkDoesNotExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, account.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkIsAlreadyInactive()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var inactiveLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(account.Id)
            .WithActive(false)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(inactiveLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, account.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(null).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid(), Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, Guid.NewGuid()));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenAccountIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UnassignAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IAccountsRepository accountsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new UnassignAccountUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);
    }
}
