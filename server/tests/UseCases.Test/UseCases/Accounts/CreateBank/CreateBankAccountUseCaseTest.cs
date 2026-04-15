using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.CreateBank;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.CreateBank;

public class CreateBankAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateBankAccount_WhenRequestIsValid()
    {
        var branchUser = new CommonTestUtilities.Entities.BranchUserBuilder().Build();
        var request = new RequestCreateBankAccountJsonBuilder()
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
        await accountsRepo.Received(1).Add(Arg.Is<Account>(account =>
            account.Type == AccountType.BankAccount &&
            account.BranchId == branchUser.BranchId));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new CommonTestUtilities.Entities.BranchUserBuilder().Build();
        var request = new RequestCreateBankAccountJsonBuilder()
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

    private static CreateBankAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new CreateBankAccountUseCase(authService, accountsRepo, unitOfWork);
    }
}
