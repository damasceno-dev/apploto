using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.OperatorAccounts.SetPrimaryAccount;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.OperatorAccounts.SetPrimaryAccount;

public class SetPrimaryAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldSetPrimary_WhenNoPreviousPrimaryExists()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var link = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(account.Id)
            .WithIsPrimary(false)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(op.Id, account.Id, link)
            .GetActivePrimaryByOperatorId(op.Id, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, account.Id);

        link.IsPrimary.ShouldBeTrue();
        response.IsPrimary.ShouldBeTrue();
        response.AccountId.ShouldBe(account.Id);
        await operatorsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId);
        await accountsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).GetByOperatorIdAndAccountId(op.Id, account.Id);
        await operatorAccountsRepo.Received(1).GetActivePrimaryByOperatorId(op.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearPreviousPrimaryAndSetNew_WhenDifferentPrimaryExists()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var previousAccount = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var newAccount = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var previousPrimary = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(previousAccount.Id)
            .WithIsPrimary(true)
            .WithActive(true)
            .Build();
        var newLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(newAccount.Id)
            .WithIsPrimary(false)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(newAccount.Id, branchUser.BranchId, newAccount)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(op.Id, newAccount.Id, newLink)
            .GetActivePrimaryByOperatorId(op.Id, previousPrimary)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, newAccount.Id);

        previousPrimary.IsPrimary.ShouldBeFalse();
        newLink.IsPrimary.ShouldBeTrue();
        response.IsPrimary.ShouldBeTrue();
        await operatorAccountsRepo.Received(1).GetByOperatorIdAndAccountId(op.Id, newAccount.Id);
        await operatorAccountsRepo.Received(1).GetActivePrimaryByOperatorId(op.Id);
        await unitOfWork.Received(2).Commit();
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenSettingAlreadyPrimaryLink()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var alreadyPrimary = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccountId(account.Id)
            .WithIsPrimary(true)
            .WithActive(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(op.Id, account.Id, alreadyPrimary)
            .GetActivePrimaryByOperatorId(op.Id, alreadyPrimary)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, account.Id);

        alreadyPrimary.IsPrimary.ShouldBeTrue();
        response.IsPrimary.ShouldBeTrue();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var accountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, null)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, accountId));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await accountsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId);
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
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(op.Id, account.Id, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, account.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
        await operatorAccountsRepo.Received(1).GetByOperatorIdAndAccountId(op.Id, account.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenLinkIsInactive()
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
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(op.Id, branchUser.BranchId, op)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(op.Id, account.Id, inactiveLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, account.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);
        await operatorAccountsRepo.Received(1).GetByOperatorIdAndAccountId(op.Id, account.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var operatorId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId, null)
            .Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(operatorId, accountId));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await operatorsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(operatorId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenEitherIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static SetPrimaryAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IAccountsRepository accountsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new SetPrimaryAccountUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);
    }
}
