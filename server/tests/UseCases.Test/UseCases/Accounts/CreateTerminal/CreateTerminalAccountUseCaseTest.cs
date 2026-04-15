using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.CreateTerminal;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.CreateTerminal;

public class CreateTerminalAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateTerminalWithoutTab_WhenRequestHasNoTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateTerminalAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.Type.ShouldBe(AccountType.Terminal);
        response.TabAccountId.ShouldBeNull();
        await accountsRepo.Received(1).Add(Arg.Is<Account>(account =>
            account.Type == AccountType.Terminal &&
            account.TabAccountId == null));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTerminalWithExistingTab_WhenExistingTabIsValid()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.TabAccountId.ShouldBe(tabAccount.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTerminalWithNewTab_WhenCreateTabAccountIsTrue()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithName("Jennifer")
            .WithInstitution("Loterica")
            .WithNumber("2")
            .WithCreateTabAccount(true)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.TabAccountId.ShouldNotBeNull();
        await accountsRepo.Received(1).Add(Arg.Is<Account>(account =>
            account.Type == AccountType.Tab &&
            account.Id == response.TabAccountId!.Value &&
            account.Name == "Jennifer" &&
            account.Institution == "Loterica" &&
            account.Number == "2"));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenExistingTabIsMissing()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(Guid.NewGuid())
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenExistingTabIsAlreadyLinked()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .GetActiveTerminalIdByTabAccountId(Guid.NewGuid())
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateTerminalAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new CreateTerminalAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
