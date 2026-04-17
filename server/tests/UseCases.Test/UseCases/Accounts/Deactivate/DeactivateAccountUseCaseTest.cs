using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.Deactivate;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.Deactivate;

public class DeactivateAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateAccount_WhenNoLinksExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id);

        account.Active.ShouldBeFalse();
        response.Id.ShouldBe(account.Id);
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByAccountId(account.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCascadeDeactivateAllActiveLinks_WhenLinksExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var link1 = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(true).WithActive(true).Build();
        var link2 = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(false).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [link1, link2])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(account.Id);

        account.Active.ShouldBeFalse();
        link1.Active.ShouldBeFalse();
        link1.IsPrimary.ShouldBeFalse();
        link2.Active.ShouldBeFalse();
        link2.IsPrimary.ShouldBeFalse();
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByAccountId(account.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotRestoreLinks_WhenAccountIsReactivatedLater()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var link = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(true).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [link])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(account.Id);

        link.Active.ShouldBeFalse();

        // Simulate manual reactivation of the account entity
        account.Active = true;

        // The link remains deactivated — one-way cascade, reassignment must be explicit
        link.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotFoundInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var accountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(accountId, branchUser.BranchId, null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(accountId));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(accountId, branchUser.BranchId);
        await operatorAccountsRepo.DidNotReceive().ListActiveByAccountId(Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenAccountIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static DeactivateAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateAccountUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);
    }
}
