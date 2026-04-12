using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.OperatorAccounts.AssignAccount;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.OperatorAccounts.AssignAccount;

public class AssignAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateNewLink_WhenNoExistingLink()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(account.Id).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        response.OperatorId.ShouldBe(op.Id);
        response.AccountId.ShouldBe(account.Id);
        response.IsPrimary.ShouldBeFalse();
        response.AccountName.ShouldBe(account.Name);
        response.AccountType.ShouldBe(account.Type);
        await operatorAccountsRepo.Received(1).Add(Arg.Is<OperatorAccount>(link =>
            link.OperatorId == op.Id &&
            link.AccountId == account.Id &&
            link.IsPrimary == false &&
            link.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReactivateExistingLink_WhenLinkIsInactive()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var inactiveLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(account)
            .WithActive(false)
            .Build();
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(account.Id).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(inactiveLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(op.Id, request);

        response.AccountId.ShouldBe(account.Id);
        inactiveLink.Active.ShouldBeTrue();
        await operatorAccountsRepo.DidNotReceive().Add(Arg.Any<OperatorAccount>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenLinkAlreadyActive()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var activeLink = new OperatorAccountBuilder()
            .WithOperatorId(op.Id)
            .WithAccount(account)
            .WithActive(true)
            .Build();
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(account.Id).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .GetByOperatorIdAndAccountId(activeLink)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(op.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ACCOUNT_ALREADY_ACTIVE);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenOperatorNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(account.Id).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(null).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.Message.ShouldBe(ResourcesErrorMessages.OPERATOR_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(Guid.NewGuid()).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(op).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchIdAsNoTracking(null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(op.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenOperatorIdIsEmpty()
    {
        var request = new RequestAssignAccountJsonBuilder().Build();

        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenAccountIdIsEmpty()
    {
        var request = new RequestAssignAccountJsonBuilder().WithAccountId(Guid.Empty).Build();

        var authService = new AuthenticationServiceBuilder().Build();
        var operatorsRepo = new OperatorsRepositoryBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static AssignAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IOperatorsRepository operatorsRepo,
        IAccountsRepository accountsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork)
    {
        return new AssignAccountUseCase(authService, operatorsRepo, accountsRepo, operatorAccountsRepo, unitOfWork);
    }
}
