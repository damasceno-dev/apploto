using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.Create;

public class CreateAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateTerminalAccount_WhenNoTabAccountId()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(null)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.Type.ShouldBe(AccountType.Terminal);
        response.Name.ShouldBe(request.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        response.TabAccountId.ShouldBeNull();
        await accountsRepo.Received(1).Add(Arg.Is<Account>(a =>
            a.Type == AccountType.Terminal &&
            a.Name == request.Name.Trim() &&
            a.BranchId == branchUser.BranchId &&
            a.TabAccountId == null &&
            a.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateBankAccount_WhenTypeBankAccount()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.BankAccount)
            .WithInstitution("Banco X")
            .WithNumber("001")
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.Type.ShouldBe(AccountType.BankAccount);
        response.Institution.ShouldBe("Banco X");
        response.Number.ShouldBe("001");
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTabAccount_WhenTypeTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Tab)
            .WithTabAccountId(null)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.Type.ShouldBe(AccountType.Tab);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTerminalWithTab_WhenTabAccountIsValidAndUnlinked()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .ExistsActiveTerminalForTabAccount(false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.TabAccountId.ShouldBe(tabAccount.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTabAccountNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(Guid.NewGuid())
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
    public async Task Execute_ShouldThrowNotFoundException_WhenReferencedAccountIsNotTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var nonTabAccount = new AccountBuilder()
            .WithType(AccountType.BankAccount)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(nonTabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(nonTabAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenTabAlreadyLinkedToActiveTerminal()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .ExistsActiveTerminalForTabAccount(true)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateAccountJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new CreateAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
