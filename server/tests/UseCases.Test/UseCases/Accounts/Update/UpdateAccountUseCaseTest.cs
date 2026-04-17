using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.Update;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.Update;

public class UpdateAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateName_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Old Name")
            .WithType(AccountType.Terminal)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithName("New Name")
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Name.ShouldBe("New Name");
        response.Name.ShouldBe("New Name");
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateOptionalFields_WhenProvided()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.BankAccount)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithInstitution("Banco Novo")
            .WithNumber("999-0")
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Institution.ShouldBe("Banco Novo");
        account.Number.ShouldBe("999-0");
        response.Institution.ShouldBe("Banco Novo");
        response.Number.ShouldBe("999-0");
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldPreserveAccountTypeAndTabLink_AfterUpdate()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccountId = Guid.NewGuid();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .WithTabAccountId(tabAccountId)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Type.ShouldBe(AccountType.Terminal);
        account.TabAccountId.ShouldBe(tabAccountId);
        response.Type.ShouldBe(AccountType.Terminal);
        response.TabAccountId.ShouldBe(tabAccountId);
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenAccountIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var accountId = Guid.NewGuid();
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(accountId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(accountId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(accountId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new UpdateAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
