using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.CreateTab;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.CreateTab;

public class CreateTabAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateTabAndPairToTerminal_WhenTerminalIsProvided()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithName("Terminal 01")
            .WithInstitution("Loterica")
            .WithNumber("1")
            .Build();
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var response = await useCase.Execute(request);

        response.TerminalAccountId.ShouldBe(terminalAccount.Id);
        terminalAccount.TabAccountId.ShouldBe(response.Id);
        await accountsRepo.Received(1).Add(Arg.Is<Account>(account =>
            account.Type == AccountType.Tab &&
            account.Name == terminalAccount.Name &&
            account.Institution == terminalAccount.Institution &&
            account.Number == terminalAccount.Number &&
            account.BranchId == branchUser.BranchId));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTerminalIsMissing()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(Guid.NewGuid())
            .Build();

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
    public async Task Execute_ShouldThrowConflictException_WhenTerminalAlreadyHasTab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(Guid.NewGuid())
            .Build();
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(terminalAccount.Id)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminalAccount)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ALREADY_LINKED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateTabAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new CreateTabAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
