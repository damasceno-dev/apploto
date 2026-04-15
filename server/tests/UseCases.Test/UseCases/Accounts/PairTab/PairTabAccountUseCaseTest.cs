using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.PairTab;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.PairTab;

public class PairTabAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldPairTerminalAndTab_WhenBothAreValid()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        terminalAccount.TabAccountId.ShouldBe(tabAccount.Id);
        response.TabAccountId.ShouldBe(tabAccount.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenTerminalAlreadyPairedWithRequestedTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(tabAccount.Id)
            .Build();
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .GetActiveTerminalIdByTabAccountId(terminalAccount.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.TabAccountId.ShouldBe(tabAccount.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTerminalAccountIsMissing()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestPairTabAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTabAccountIsMissing()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .GetActiveByIdAndBranchIdAsNoTracking(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenTerminalAlreadyHasDifferentTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(Guid.NewGuid())
            .Build();
        var requestedTabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .WithTabAccountId(requestedTabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .GetActiveByIdAndBranchIdAsNoTracking(requestedTabAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenTabAlreadyBelongsToAnotherTerminal()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .WithTabAccountId(tabAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .GetActiveByIdAndBranchIdAsNoTracking(tabAccount)
            .GetActiveTerminalIdByTabAccountId(Guid.NewGuid())
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static PairTabAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new PairTabAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
