using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Update;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Transactions.Update;

/// <summary>
/// Use-case tests verify orchestration: load → guards → validators → mutate → commit.
/// The shared <see cref="ITransactionMutationPermissionGuard"/> and
/// <see cref="IMemberAccountScopeResolver"/> are substituted so the role × link ×
/// recording-operator × same-day decision tree is exercised once, in
/// <c>TransactionMutationPermissionGuardTest</c>, not duplicated here.
/// </summary>
public class UpdateTransactionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateEditableSubsetAndStampAudit_WhenAllGuardsPass()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var beforeUpdate = DateTime.UtcNow;
        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version);
        var afterUpdate = DateTime.UtcNow;

        response.Id.ShouldBe(ctx.Transaction.Id);
        response.Description.ShouldBe(ctx.Request.Description);
        response.DueDate.ShouldBe(ctx.Request.DueDate);
        response.ClientId.ShouldBe(ctx.Request.ClientId);
        response.TransactionTime.ShouldBe(ctx.Request.TransactionTime);
        response.UpdatedAt.ShouldNotBeNull();
        response.UpdatedAt.Value.ShouldBeGreaterThanOrEqualTo(beforeUpdate);
        response.UpdatedAt.Value.ShouldBeLessThanOrEqualTo(afterUpdate);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        ctx.Transaction.Description.ShouldBe(ctx.Request.Description);
        ctx.Transaction.DueDate.ShouldBe(ctx.Request.DueDate);
        ctx.Transaction.ClientId.ShouldBe(ctx.Request.ClientId);
        ctx.Transaction.TransactionTime.ShouldBe(ctx.Request.TransactionTime);
        ctx.Transaction.UpdatedAt.ShouldBe(response.UpdatedAt);
        ctx.Transaction.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        await ctx.TransactionsRepository.Received(1)
            .GetByIdAndBranchId(ctx.Transaction.Id, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateDraftTransaction_WhenAllGuardsPass()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.Status = TransactionStatus.Draft;
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("Draft after edit")
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 20))
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder().Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version);

        response.Status.ShouldBe(TransactionStatus.Draft);
        response.Description.ShouldBe("Draft after edit");
        response.TransactionTime.ShouldBe(new TimeOnly(10, 20));
        ctx.Transaction.Status.ShouldBe(TransactionStatus.Draft);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearNullableEditableFields_WhenRequestSuppliesNulls()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(null)
            .WithDueDate(ctx.Transaction.Date.AddDays(2))
            .WithClientId(null)
            .WithTransactionTime(null)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder().Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version);

        response.Description.ShouldBeNull();
        response.ClientId.ShouldBeNull();
        response.TransactionTime.ShouldBeNull();
        ctx.Transaction.Description.ShouldBeNull();
        ctx.Transaction.ClientId.ShouldBeNull();
        ctx.Transaction.TransactionTime.ShouldBeNull();
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDelegateToMutationGuard_WithLoadedTransactionAndAuthenticatedUserAndCallerOperator()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version);

        ctx.MutationPermissionGuard.Received(1).EnsureAllowed(
            ctx.Transaction,
            ctx.BranchUser.Role,
            ctx.CallerOperator,
            Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Execute_ShouldRethrowMutationGuardException_AndNotCommit()
    {
        var ctx = BuildContext(Role.Member);
        ctx.MutationPermissionGuard
            .When(guard => guard.EnsureAllowed(
                Arg.Any<Transaction>(),
                Arg.Any<Role>(),
                Arg.Any<Operator?>(),
                Arg.Any<DateTime>()))
            .Do(_ => throw new TokenWithoutPermissionException(
                ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRunMutationGuardBeforeMemberAccountScopeGuard_ForMemberWithNoLinkedOperator()
    {
        // 5.5.18: a Member with no linked operator must surface the mutation guard's
        // no-link key, not the account-scope guard's out-of-scope key. The use case
        // calls the mutation guard first; the account-scope guard never runs in this
        // path because the mutation guard short-circuits.
        var ctx = BuildContext(Role.Member);
        ctx.MemberAccountScopeResolver
            .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
            .Returns(new MemberAccountScope(LinkedOperator: null, AllowedAccountIds: []));
        ctx.MutationPermissionGuard
            .When(guard => guard.EnsureAllowed(
                Arg.Any<Transaction>(),
                Arg.Any<Role>(),
                null,
                Arg.Any<DateTime>()))
            .Do(_ => throw new TokenWithoutPermissionException(
                ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        ctx.MutationPermissionGuard.Received(1).EnsureAllowed(
            ctx.Transaction,
            Role.Member,
            null,
            Arg.Any<DateTime>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenBeforeMutationGuard_WhenMemberHasLinkedOperatorButNoActiveAccounts()
    {
        // 5.5.14: a linked Member with zero active account links is account-scope
        // denied before mutation-specific rules such as same-day or recording operator.
        var ctx = BuildContext(Role.Member);
        ctx.MemberAccountScopeResolver
            .Resolve(ctx.BranchUser.UserId, ctx.BranchUser.BranchId)
            .Returns(new MemberAccountScope(LinkedOperator: ctx.CallerOperator, AllowedAccountIds: []));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        ctx.MutationPermissionGuard.DidNotReceiveWithAnyArgs()
            .EnsureAllowed(default!, default, default, default);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenDueDateIsBeforeTransactionDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(-1))
            .WithClientId(ctx.Request.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
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
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenTransactionIsCancelled()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.Status = TransactionStatus.Cancelled;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
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
            () => useCase.Execute(transactionId, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
        await ctx.TransactionsRepository.Received(1).GetByIdAndBranchIdAsNoTracking(
            transactionId,
            ctx.BranchUser.BranchId,
            Arg.Any<CancellationToken>());
        await ctx.TransactionsRepository.DidNotReceive().GetByIdAndBranchId(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoTransactionClearsClient()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.TransactionType.RequiresTabAccountAndClient = true;
        ctx.Transaction.ClientId = Guid.NewGuid();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithClientId(null)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder().Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenRequestClientDoesNotExistInBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var missingClientId = Guid.NewGuid();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(ctx.Request.Description)
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithClientId(missingClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await ctx.ClientsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRefuseAClientChange_WhenTheReceivableHasCommandSettlementEvents()
    {
        // M9.7 review P1: a deposit/return already identified who owes the cheque; editing the row must not move it.
        var ctx = BuildContext(Role.Manager);
        ctx.SettlementEventsRepository = new SettlementEventsRepositoryBuilder()
            .ExistsCommandEventReturns(ctx.Transaction.Id, true)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.RECEIVABLE_CLIENT_LOCKED_BY_EVENTS);
        await ctx.SettlementEventsRepository.Received(1)
            .ExistsCommandEventByReceivableIdAsNoTracking(ctx.Transaction.Id, Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowOtherEdits_WhenTheClientIsUnchangedOnAReceivableWithEvents()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.SettlementEventsRepository = new SettlementEventsRepositoryBuilder()
            .ExistsCommandEventReturns(ctx.Transaction.Id, true)
            .Build();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("nova descrição")
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithClientId(ctx.Transaction.ClientId)
            .WithTransactionTime(ctx.Request.TransactionTime)
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(
                ctx.Transaction.ClientId!.Value,
                ctx.BranchUser.BranchId,
                new ClientBuilder().WithId(ctx.Transaction.ClientId.Value).WithBranchId(ctx.BranchUser.BranchId).Build())
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.Transaction.Version);

        response.Description.ShouldBe("nova descrição");
        await ctx.UnitOfWork.Received().Commit(Arg.Any<CancellationToken>());
    }

    private static UpdateTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(new LockDateReader(ctx.SettingsRepository));

        return new UpdateTransactionUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.ClientsRepository,
            ctx.UnitOfWork,
            ctx.MemberAccountScopeResolver,
            memberAccountScopeGuard,
            ctx.MutationPermissionGuard,
            lockDateGuard,
            new MonthLockCoordinationBuilder().Build(),
            ctx.SettlementEventsRepository);
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
            .WithRequiresTabAccountAndClient(false)
            .Build();
        var currentClientId = Guid.NewGuid();
        var requestClientId = Guid.NewGuid();
        var transactionDate = DateTime.UtcNow.Date;
        var transaction = new TransactionBuilder()
            .WithBranch(branch)
            .WithTransactionType(transactionType)
            .WithDate(transactionDate)
            .WithDueDate(transactionDate)
            .WithDescription("Original description")
            .WithClientId(currentClientId)
            .WithTransactionTime(new TimeOnly(9, 15))
            .WithRecordedByOperatorId(callerOperator.Id)
            .Build();
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("Updated description")
            .WithDueDate(transactionDate.AddDays(2))
            .WithClientId(requestClientId)
            .WithTransactionTime(new TimeOnly(15, 45))
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdReturns(transaction.Id, branchUser.BranchId, transaction)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(
                requestClientId,
                branchUser.BranchId,
                new ClientBuilder().WithId(requestClientId).WithBranchId(branchUser.BranchId).Build())
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
            Request = request,
            AuthenticationService = authenticationService,
            TransactionsRepository = transactionsRepository,
            ClientsRepository = clientsRepository,
            SettingsRepository = settingsRepository,
            MemberAccountScopeResolver = memberAccountScopeResolver,
            MutationPermissionGuard = mutationPermissionGuard,
            UnitOfWork = unitOfWork
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator CallerOperator { get; init; }
        public required Transaction Transaction { get; set; }
        public required RequestUpdateTransactionJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IMemberAccountScopeResolver MemberAccountScopeResolver { get; init; }
        public required ITransactionMutationPermissionGuard MutationPermissionGuard { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
        public ISettlementEventsRepository SettlementEventsRepository { get; set; } = new SettlementEventsRepositoryBuilder().Build();
    }
}
