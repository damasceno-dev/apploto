using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using server.Application.UseCases.Accounts.List;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.List;

public class ListAccountsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnAccounts_WhenBranchHasActiveAccounts()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminalAccount = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Tab)
            .Build();
        var accounts = new List<server.Domain.Entities.Account>
        {
            terminalAccount,
            tabAccount
        };

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .ListActiveByBranchId(accounts)
            .ListActiveTerminalIdsByTabAccountIds(new Dictionary<Guid, Guid> { [tabAccount.Id] = terminalAccount.Id })
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var response = await useCase.Execute();

        response.Accounts.ShouldNotBeEmpty();
        response.Accounts.Count.ShouldBe(2);
        response.Accounts.ShouldAllBe(a => a.BranchId == branchUser.BranchId);
        response.Accounts.Single(a => a.Id == terminalAccount.Id).TerminalAccountId.ShouldBeNull();
        response.Accounts.Single(a => a.Id == tabAccount.Id).TerminalAccountId.ShouldBe(terminalAccount.Id);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenBranchHasNoActiveAccounts()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .ListActiveByBranchId([])
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var response = await useCase.Execute();

        response.Accounts.ShouldBeEmpty();
    }

    private static ListAccountsUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo)
    {
        return new ListAccountsUseCase(authService, accountsRepo);
    }
}
