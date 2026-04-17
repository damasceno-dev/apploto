using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.Get;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.Get;

public class GetAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnAccount_WhenAccountExistsInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranchId(branchUser.BranchId)
            .Build();
        var terminalAccountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId, account)
            .GetActiveTerminalIdByTabAccountId(account.Id, branchUser.BranchId, terminalAccountId)
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var response = await useCase.Execute(account.Id);

        response.Id.ShouldBe(account.Id);
        response.Name.ShouldBe(account.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        response.TerminalAccountId.ShouldBe(terminalAccountId);
        await accountsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(account.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var accountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(accountId));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await accountsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId);
    }

    private static GetAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo)
    {
        return new GetAccountUseCase(authService, accountsRepo);
    }
}
