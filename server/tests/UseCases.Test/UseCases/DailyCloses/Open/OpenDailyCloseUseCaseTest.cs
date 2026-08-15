using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.UseCases.DailyCloses.Open;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.DailyCloses.Open;

public class OpenDailyCloseUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateUnclaimedDraftWithOpener_WhenManagerHasLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(DailyCloseStatus.Draft);
        response.AccountId.ShouldBe(ctx.Request.AccountId);
        response.BranchId.ShouldBe(ctx.BranchUser.BranchId);
        response.OpenedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.RecordedByUserId.ShouldBeNull();
        response.RecordedByOperatorId.ShouldBeNull();
        response.SubmittedByUserId.ShouldBeNull();
        response.SubmittedByOperatorId.ShouldBeNull();

        await ctx.DailyClosesRepository.Received(1).Add(Arg.Is<DailyClose>(dc =>
            dc.BranchId == ctx.BranchUser.BranchId &&
            dc.AccountId == ctx.Request.AccountId &&
            dc.Date == ctx.Request.Date &&
            dc.Status == DailyCloseStatus.Draft &&
            dc.OpenedByUserId == ctx.BranchUser.UserId &&
            dc.RecordedByUserId == null &&
            dc.SubmittedByUserId == null));
        await ctx.UnitOfWork.Received(1).Commit();

        ctx.WorkflowGuard.Received(1).EnsureCanOpen(
            ctx.BranchUser,
            ctx.CallerOperator,
            ctx.Request.Date);
    }

    [Fact]
    public async Task Execute_ShouldCreateUnclaimedDraft_WhenAdminHasNoLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Admin);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(DailyCloseStatus.Draft);
        response.OpenedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.RecordedByUserId.ShouldBeNull();
        response.SubmittedByOperatorId.ShouldBeNull();

        await ctx.DailyClosesRepository.Received(1).Add(Arg.Is<DailyClose>(dc =>
            dc.SubmittedByOperatorId == null));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDelegateToWorkflowGuard_WhenMemberHasLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Request);

        ctx.WorkflowGuard.Received(1).EnsureCanOpen(
            ctx.BranchUser,
            ctx.CallerOperator,
            ctx.Request.Date);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenAccountMissingOrCrossBranch()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.AccountId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Member);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberTargetsUnlinkedAccount()
    {
        var ctx = BuildHappyPathContext(Role.Member);
        // Operator exists but has no linked accounts.
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator.Id, [])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksTargetDate()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.Request.Date
                })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldPassExactBranchScopedArgsToRepositories()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Request);

        await ctx.OperatorsRepository.Received(1)
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId);
        await ctx.AccountsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(
                ctx.Request.AccountId,
                ctx.BranchUser.BranchId,
                Arg.Any<CancellationToken>());
        await ctx.AccountsRepository.Received(1)
            .GetActiveByIdAndBranchId(
                ctx.Request.AccountId,
                ctx.BranchUser.BranchId,
                Arg.Any<CancellationToken>());
    }

    private static OpenDailyCloseUseCase CreateUseCase(HappyPathContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(new LockDateReader(ctx.SettingsRepository));
        var coordination = new DailyCloseAccountCoordinationBuilder().Build();

        return new OpenDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.AccountsRepository,
            memberAccountScopeResolver,
            memberAccountScopeGuard,
            ctx.WorkflowGuard,
            lockDateGuard,
            ctx.DailyClosesRepository,
            coordination,
            ctx.UnitOfWork);
    }

    private static HappyPathContext BuildHappyPathContext(Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var accountId = Guid.NewGuid();
        var account = new AccountBuilder()
            .WithId(accountId)
            .WithBranchId(branchUser.BranchId)
            .WithType(AccountType.Terminal)
            .Build();
        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(account)
            .Build();

        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(DateTime.Today)
            .WithAccountId(accountId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();
        var accountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, account)
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();

        var workflowGuard = Substitute.For<IDailyCloseWorkflowGuard>();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new HappyPathContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            Account = account,
            Request = request,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            AccountsRepository = accountsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            SettingsRepository = settingsRepository,
            WorkflowGuard = workflowGuard,
            DailyClosesRepository = dailyClosesRepository,
            UnitOfWork = unitOfWork
        };
    }

    private class HappyPathContext
    {
        public required BranchUser BranchUser { get; set; }
        public required Operator CallerOperator { get; set; }
        public required Account Account { get; set; }
        public required RequestOpenDailyCloseJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required IUnitOfWork UnitOfWork { get; set; }
    }
}
