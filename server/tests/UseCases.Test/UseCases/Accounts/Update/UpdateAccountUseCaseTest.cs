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
            .WithTabAccountId(null)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Name.ShouldBe("New Name");
        response.Name.ShouldBe("New Name");
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
            .WithTabAccountId(null)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Institution.ShouldBe("Banco Novo");
        account.Number.ShouldBe("999-0");
        response.Institution.ShouldBe("Banco Novo");
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldPreserveAccountType_AfterUpdate()
    {
        // Type is immutable — the update request has no Type field,
        // so the account's type must remain unchanged.
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.BankAccount)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(account)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id, request);

        account.Type.ShouldBe(AccountType.BankAccount);
        response.Type.ShouldBe(AccountType.BankAccount);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateTabAccountId_WhenTerminalAndTabIsValidAndUnlinked()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminal = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Tab)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminal)
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .ExistsActiveTerminalForTabAccount(false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(terminal.Id, request);

        terminal.TabAccountId.ShouldBe(tabAccount.Id);
        response.TabAccountId.ShouldBe(tabAccount.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenTabAccountIdSetOnNonTerminal()
    {
        var branchUser = new BranchUserBuilder().Build();
        var bankAccount = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.BankAccount)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(bankAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(bankAccount.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ID_ONLY_FOR_TERMINAL);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTabAccountNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminal = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminal)
            .GetActiveByIdAndBranchIdAsNoTracking(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(terminal.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTabAccountIsNotTypeTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminal = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var anotherTerminal = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithTabAccountId(anotherTerminal.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminal)
            .GetActiveByIdAndBranchIdAsNoTracking(anotherTerminal)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(terminal.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenTabAlreadyLinkedToAnotherTerminal()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminal = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Tab)
            .Build();
        var request = new RequestUpdateAccountJsonBuilder()
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminal)
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .ExistsActiveTerminalForTabAccount(true)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(terminal.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
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
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
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
