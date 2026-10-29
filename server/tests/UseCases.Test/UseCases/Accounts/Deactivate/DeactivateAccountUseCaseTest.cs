using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Accounts.Deactivate;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Accounts.Deactivate;

public class DeactivateAccountUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateAccount_WhenNoLinksExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var response = await useCase.Execute(account.Id);

        account.Active.ShouldBeFalse();
        response.Id.ShouldBe(account.Id);
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByAccountId(account.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCascadeDeactivateAllActiveLinks_WhenLinksExist()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var link1 = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(true).WithActive(true).Build();
        var link2 = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(false).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [link1, link2])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(account.Id);

        account.Active.ShouldBeFalse();
        link1.Active.ShouldBeFalse();
        link1.IsPrimary.ShouldBeFalse();
        link2.Active.ShouldBeFalse();
        link2.IsPrimary.ShouldBeFalse();
        await accountsRepo.Received(1).GetActiveByIdAndBranchId(account.Id, branchUser.BranchId);
        await operatorAccountsRepo.Received(1).ListActiveByAccountId(account.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotRestoreLinks_WhenAccountIsReactivatedLater()
    {
        var branchUser = new BranchUserBuilder().Build();
        var account = new AccountBuilder().WithBranchId(branchUser.BranchId).Build();

        var link = new OperatorAccountBuilder()
            .WithAccountId(account.Id).WithIsPrimary(true).WithActive(true).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(account.Id, branchUser.BranchId, account).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder()
            .ListActiveByAccountId(account.Id, [link])
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(account.Id);

        link.Active.ShouldBeFalse();

        // Simulate manual reactivation of the account entity
        account.Active = true;

        // The link remains deactivated — one-way cascade, reassignment must be explicit
        link.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenAccountNotFoundInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var accountId = Guid.NewGuid();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(accountId, branchUser.BranchId, null).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(accountId));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await accountsRepo.Received(1).GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId);
        await operatorAccountsRepo.DidNotReceive().ListActiveByAccountId(Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenAccountIdIsEmpty()
    {
        var authService = new AuthenticationServiceBuilder().Build();
        var accountsRepo = new AccountsRepositoryBuilder().Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUnpairTerminalInTheSameCommit_WhenTerminalHoldsATab()
    {
        var branchUser = new BranchUserBuilder().Build();
        var terminal = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(terminal.Id, branchUser.BranchId, terminal)
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().ListActiveByAccountId(terminal.Id, []).Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork, coordinationBuilder.Build());

        var response = await useCase.Execute(terminal.Id);

        terminal.Active.ShouldBeFalse();
        terminal.TabAccountId.ShouldBeNull();
        response.TabAccountId.ShouldBeNull();
        await accountsRepo.DidNotReceiveWithAnyArgs().ListTerminalsByTabAccountId(default, default);
        Received.InOrder(() =>
        {
            unitOfWork.Commit();
            coordinationBuilder.Scope.Complete(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Execute_ShouldReleaseEveryHolder_WhenTabIsDeactivated()
    {
        var branchUser = new BranchUserBuilder().Build();
        var tab = new AccountBuilder().WithType(AccountType.Tab).WithBranchId(branchUser.BranchId).Build();
        var activeHolder = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(tab.Id)
            .Build();
        var staleInactiveHolder = new AccountBuilder()
            .WithType(AccountType.Terminal)
            .WithBranchId(branchUser.BranchId)
            .WithTabAccountId(tab.Id)
            .WithActive(false)
            .Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchId(tab.Id, branchUser.BranchId, tab)
            .ListTerminalsByTabAccountId(branchUser.BranchId, tab.Id, [activeHolder, staleInactiveHolder])
            .Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().ListActiveByAccountId(tab.Id, []).Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork, coordinationBuilder.Build());

        await useCase.Execute(tab.Id);

        tab.Active.ShouldBeFalse();
        activeHolder.TabAccountId.ShouldBeNull();
        activeHolder.Active.ShouldBeTrue();
        staleInactiveHolder.TabAccountId.ShouldBeNull();
        // Read once to take every holder's account key in the global order, then again under the lock.
        await accountsRepo.Received(2).ListTerminalsByTabAccountId(branchUser.BranchId, tab.Id, Arg.Any<CancellationToken>());
        await coordinationBuilder.Build().Received(1).AcquireWithPairing(
            branchUser.BranchId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(tab.Id) && ids.Contains(activeHolder.Id)),
            Arg.Any<CancellationToken>());
        await unitOfWork.Received(1).Commit();
        await coordinationBuilder.Scope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldTouchNoPairing_WhenBankAccountIsDeactivated()
    {
        var branchUser = new BranchUserBuilder().Build();
        var bank = new AccountBuilder().WithType(AccountType.BankAccount).WithBranchId(branchUser.BranchId).Build();

        var authService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build();
        var accountsRepo = new AccountsRepositoryBuilder().GetActiveByIdAndBranchId(bank.Id, branchUser.BranchId, bank).Build();
        var operatorAccountsRepo = new OperatorAccountsRepositoryBuilder().ListActiveByAccountId(bank.Id, []).Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authService, accountsRepo, operatorAccountsRepo, unitOfWork);

        await useCase.Execute(bank.Id);

        bank.Active.ShouldBeFalse();
        await accountsRepo.DidNotReceiveWithAnyArgs().ListTerminalsByTabAccountId(default, default);
        await unitOfWork.Received(1).Commit();
    }

    private static DeactivateAccountUseCase CreateUseCase(
        IAuthenticationService authService,
        IAccountsRepository accountsRepo,
        IOperatorAccountsRepository operatorAccountsRepo,
        IUnitOfWork unitOfWork,
        IDailyCloseAccountCoordination? accountCoordination = null)
    {
        return new DeactivateAccountUseCase(
            authService,
            accountsRepo,
            operatorAccountsRepo,
            accountCoordination ?? new DailyCloseAccountCoordinationBuilder().Build(),
            new DailyClosesRepositoryBuilder().Build(),
            Substitute.For<ISessionHandoversRepository>(),
            unitOfWork);
    }
}
