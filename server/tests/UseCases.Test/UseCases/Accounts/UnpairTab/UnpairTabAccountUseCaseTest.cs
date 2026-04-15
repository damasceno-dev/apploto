using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.UnpairTab;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.UnpairTab;

public class UnpairTabAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldClearTabAccountId_WhenTerminalHasTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(terminalAccount.Id);

        terminalAccount.TabAccountId.ShouldBeNull();
        response.TabAccountId.ShouldBeNull();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldBeIdempotent_WhenTerminalHasNoTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(null)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(terminalAccount.Id);

        response.TabAccountId.ShouldBeNull();
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenTerminalAccountIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTerminalAccountIsMissing()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UnpairTabAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new UnpairTabAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
