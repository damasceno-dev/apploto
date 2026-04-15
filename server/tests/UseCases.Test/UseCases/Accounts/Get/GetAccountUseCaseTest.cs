using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
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
            .GetActiveByIdAndBranchIdAsNoTracking(account)
            .GetActiveTerminalIdByTabAccountId(terminalAccountId)
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var response = await useCase.Execute(account.Id);

        response.Id.ShouldBe(account.Id);
        response.Name.ShouldBe(account.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        response.TerminalAccountId.ShouldBe(terminalAccountId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(null)
            .Build();

        var useCase = CreateUseCase(authService, accountsRepo);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    private static GetAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo)
    {
        return new GetAccountUseCase(authService, accountsRepo);
    }
}
