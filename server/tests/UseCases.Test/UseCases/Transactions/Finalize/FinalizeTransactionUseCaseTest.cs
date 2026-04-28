using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Finalize;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Transactions.Finalize;

public class FinalizeTransactionUseCaseTest
{
    public static TheoryData<TransactionStatus> NonDraftStatuses =>
    [
        TransactionStatus.Active,
        TransactionStatus.Cancelled
    ];

    public static TheoryData<string> MutationGuardFailureMessages =>
    [
        ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK,
        ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR,
        ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY
    ];

    [Fact]
    public async Task Execute_ShouldFinalizeDraftTransactionAndStampAudit_WhenAllGuardsPass()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var beforeUpdate = DateTime.UtcNow;
        var response = await useCase.Execute(ctx.Transaction.Id);
        var afterUpdate = DateTime.UtcNow;

        response.Id.ShouldBe(ctx.Transaction.Id);
        response.Status.ShouldBe(TransactionStatus.Active);
        response.UpdatedAt.ShouldNotBeNull();
        response.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
        response.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        ctx.Transaction.Status.ShouldBe(TransactionStatus.Active);
        ctx.Transaction.UpdatedAt.ShouldBe(response.UpdatedAt);
        ctx.Transaction.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        await ctx.TransactionsRepository.Received(1)
            .GetByIdAndBranchId(ctx.Transaction.Id, ctx.BranchUser.BranchId);
        ctx.MutationPermissionGuard.Received(1).EnsureAllowed(
            ctx.Transaction,
            ctx.BranchUser.Role,
            ctx.CallerOperator,
            Arg.Any<DateTime>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Theory]
    [MemberData(nameof(NonDraftStatuses))]
    public async Task Execute_ShouldThrowConflict_WhenTransactionIsNotDraft(TransactionStatus status)
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithStatus(status)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_CANNOT_FINALIZE_NON_DRAFT);
        ctx.MutationPermissionGuard.DidNotReceiveWithAnyArgs()
            .EnsureAllowed(null!, default, null, default);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var transactionId = Guid.NewGuid();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(transactionId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(transactionId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
        await ctx.TransactionsRepository.Received(1).GetByIdAndBranchId(transactionId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksTransactionDate()
    {
        var ctx = BuildContext(Role.Admin);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.Transaction.Date
                })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenBeforeMutationGuard_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        var ctx = BuildContext(Role.Member);
        ctx.MemberAccountScopeResolver
            .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
            .Returns(new MemberAccountScope(LinkedOperator: ctx.CallerOperator, AllowedAccountIds: []));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        ctx.MutationPermissionGuard.DidNotReceiveWithAnyArgs()
            .EnsureAllowed(null!, default, null, default);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [MemberData(nameof(MutationGuardFailureMessages))]
    public async Task Execute_ShouldPropagateMutationGuardFailures_AndNotCommit(string errorMessage)
    {
        var ctx = BuildContext(Role.Member);
        if (errorMessage == ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK)
        {
            ctx.MemberAccountScopeResolver
                .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
                .Returns(new MemberAccountScope(LinkedOperator: null, AllowedAccountIds: []));
        }

        ctx.MutationPermissionGuard
            .When(guard => guard.EnsureAllowed(
                Arg.Any<Transaction>(),
                Arg.Any<Role>(),
                Arg.Any<Operator?>(),
                Arg.Any<DateTime>()))
            .Do(_ => throw new TokenWithoutPermissionException(errorMessage));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id));

        exception.Message.ShouldBe(errorMessage);
        ctx.MutationPermissionGuard.Received(1).EnsureAllowed(
            ctx.Transaction,
            ctx.BranchUser.Role,
            errorMessage == ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK ? null : ctx.CallerOperator,
            Arg.Any<DateTime>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUseBranchClockThroughMutationGuard_WhenLocalBusinessDayDiffersFromUtcDate()
    {
        var branchClock = new PreviousUtcDayBranchClock();
        var localBusinessDate = branchClock.LocalBusinessDate(DateTime.UtcNow);
        var ctx = BuildContext(Role.Member);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(localBusinessDate)
            .WithDueDate(localBusinessDate)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.MemberAccountScopeResolver
            .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
            .Returns(new MemberAccountScope(ctx.CallerOperator, [ctx.Transaction.AccountId]));
        ctx.MutationPermissionGuard = new TransactionMutationPermissionGuard(branchClock);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id);

        response.Status.ShouldBe(TransactionStatus.Active);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldRejectMember_WhenInjectedBranchClockMapsUtcTodayToDifferentLocalBusinessDate()
    {
        var branchClock = new PreviousUtcDayBranchClock();
        var utcDate = DateTime.UtcNow.Date;
        var ctx = BuildContext(Role.Member);
        ctx.Transaction = TransactionBuilder.From(ctx.Transaction)
            .WithDate(utcDate)
            .WithDueDate(utcDate)
            .Build();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.MemberAccountScopeResolver
            .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
            .Returns(new MemberAccountScope(ctx.CallerOperator, [ctx.Transaction.AccountId]));
        ctx.MutationPermissionGuard = new TransactionMutationPermissionGuard(branchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private static FinalizeTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);

        return new FinalizeTransactionUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.MemberAccountScopeResolver,
            memberAccountScopeGuard,
            ctx.MutationPermissionGuard,
            lockDateGuard,
            new BranchClock(),
            ctx.UnitOfWork);
    }

    private static TestContext BuildContext(Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var branch = new BranchBuilder().WithId(branchUser.BranchId).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranch(branch)
            .WithUserId(branchUser.UserId)
            .Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Entradas",
            DefaultDirection = Direction.In,
            BranchId = branch.Id,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .Build();
        var transactionDate = DateTime.UtcNow.Date;
        var transaction = new TransactionBuilder()
            .WithBranch(branch)
            .WithTransactionType(transactionType)
            .WithDate(transactionDate)
            .WithDueDate(transactionDate)
            .WithStatus(TransactionStatus.Draft)
            .WithRecordedByOperatorId(callerOperator.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(transaction.Id, branchUser.BranchId, transaction)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var memberAccountScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberAccountScopeResolver
            .Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(
                LinkedOperator: callerOperator,
                AllowedAccountIds: [transaction.AccountId]));

        var mutationPermissionGuard = Substitute.For<ITransactionMutationPermissionGuard>();

        return new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            Transaction = transaction,
            AuthenticationService = authenticationService,
            TransactionsRepository = transactionsRepository,
            SettingsRepository = settingsRepository,
            MemberAccountScopeResolver = memberAccountScopeResolver,
            MutationPermissionGuard = mutationPermissionGuard,
            UnitOfWork = unitOfWork
        };
    }

    private sealed class PreviousUtcDayBranchClock : IBranchClock
    {
        public DateTime UtcNow()
        {
            return DateTime.UtcNow;
        }

        public DateTime LocalBusinessDate(DateTime utcInstant)
        {
            return utcInstant.Date.AddDays(-1);
        }

        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
        {
            return localBusinessDate.Date == LocalBusinessDate(utcInstant);
        }
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator CallerOperator { get; init; }
        public required Transaction Transaction { get; set; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IMemberAccountScopeResolver MemberAccountScopeResolver { get; init; }
        public required ITransactionMutationPermissionGuard MutationPermissionGuard { get; set; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
