using server.Application.Services.Sessions;
using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.FinalizeInstallmentPlan;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Transactions.FinalizeInstallmentPlan;

public class FinalizeInstallmentPlanUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldActivateEveryDraftUnderOneCoordinationAndCommitOnce()
    {
        var ctx = BuildContext(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.OriginId, ctx.ExpectedRequest());

        response.Status.ShouldBe(InstallmentPlanStatus.Active);
        ctx.Installments.ShouldAllBe(installment =>
            installment.Status == TransactionStatus.Active && installment.UpdatedByUserId == ctx.BranchUser.UserId);
        foreach (var installment in ctx.Installments)
            ctx.MutationPermissionGuard.Received(1).EnsureAllowed(installment, ctx.BranchUser.Role, ctx.CallerOperator, Arg.Any<DateTime>());
        await ctx.Coordination.Received(1).Acquire(ctx.BranchUser.BranchId, ctx.AccountId, Arg.Any<CancellationToken>());
        await ctx.LedgerGuard.Received(1).EnsureLedgerIsMutable(Arg.Any<Transaction>(), Arg.Any<CancellationToken>());
        await ctx.TransactionsRepository.Received(1).ListPlanInstallmentsByOriginTransactionIdAndBranchId(
            ctx.OriginId, ctx.BranchUser.BranchId, Arg.Any<CancellationToken>());
        Received.InOrder(() =>
        {
            ctx.UnitOfWork.TryCommitOptimistic(Arg.Any<CancellationToken>());
            ctx.CoordinationScope.Complete(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Execute_ShouldRefuseAStaleVersionBeforeAnyPermissionCheckOrMutation()
    {
        var ctx = BuildContext(TransactionStatus.Draft, TransactionStatus.Draft);
        var request = new RequestFinalizeInstallmentPlanJsonBuilder()
            .WithInstallments([])
            .WithInstallment(ctx.Installments[0].Id, ctx.Installments[0].Version)
            .WithInstallment(ctx.Installments[1].Id, ctx.Installments[1].Version + 1)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.OriginId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        ctx.Installments.ShouldAllBe(installment => installment.Status == TransactionStatus.Draft);
        ctx.MutationPermissionGuard.DidNotReceiveWithAnyArgs().EnsureAllowed(null!, default, null, default);
        await ctx.UnitOfWork.DidNotReceive().TryCommitOptimistic(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRequireConfirmationForAMixedPlan_AndThenActivateOnlyItsDrafts()
    {
        var ctx = BuildContext(TransactionStatus.Cancelled, TransactionStatus.Draft, TransactionStatus.Draft);
        var drafts = ctx.Installments.Where(installment => installment.Status == TransactionStatus.Draft).ToList();
        var request = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallments([]);
        foreach (var draft in drafts)
            request.WithInstallment(draft.Id, draft.Version);
        var useCase = CreateUseCase(ctx);

        var unconfirmed = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.OriginId, request.Build()));
        unconfirmed.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED);
        await ctx.UnitOfWork.DidNotReceive().TryCommitOptimistic(Arg.Any<CancellationToken>());

        var response = await useCase.Execute(ctx.OriginId, request.WithConfirmMixedPlan(true).Build());

        response.ActiveCount.ShouldBe(2);
        response.CancelledCount.ShouldBe(1);
        ctx.Installments[0].Status.ShouldBe(TransactionStatus.Cancelled);
    }

    [Fact]
    public async Task Execute_ShouldRefuseAPlanWithoutDraftInstallments()
    {
        var ctx = BuildContext(TransactionStatus.Active, TransactionStatus.Cancelled);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.OriginId, ctx.ExpectedRequest()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_DRAFT);
        await ctx.UnitOfWork.DidNotReceive().TryCommitOptimistic(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundWithoutCoordinating_WhenThePlanIsUnknown()
    {
        var ctx = BuildContext(TransactionStatus.Draft);
        var unknown = Guid.NewGuid();
        ctx.TransactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
                unknown, ctx.BranchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(unknown, ctx.ExpectedRequest()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_FOUND);
        await ctx.Coordination.DidNotReceiveWithAnyArgs().Acquire(default, default);
    }

    [Fact]
    public async Task Execute_ShouldLeaveEveryInstallmentDraft_WhenTheLedgerIsFrozen()
    {
        var ctx = BuildContext(TransactionStatus.Draft, TransactionStatus.Draft);
        ctx.LedgerGuard.EnsureLedgerIsMutable(Arg.Any<Transaction>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.OriginId, ctx.ExpectedRequest()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        ctx.Installments.ShouldAllBe(installment => installment.Status == TransactionStatus.Draft);
        await ctx.UnitOfWork.DidNotReceive().TryCommitOptimistic(Arg.Any<CancellationToken>());
        await ctx.CoordinationScope.DidNotReceive().Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldReportThePlanStaleError_WhenAnInstallmentChangesBeforeTheSave()
    {
        var ctx = BuildContext(TransactionStatus.Draft, TransactionStatus.Draft, unitOfWork: new UnitOfWorkBuilder().TryCommitOptimisticConflicts().Build());
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.OriginId, ctx.ExpectedRequest()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        await ctx.CoordinationScope.DidNotReceive().Complete(Arg.Any<CancellationToken>());
    }

    private static FinalizeInstallmentPlanUseCase CreateUseCase(TestContext ctx)
    {
        return new FinalizeInstallmentPlanUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.MemberAccountScopeResolver,
            new MemberAccountScopeGuard(),
            ctx.MutationPermissionGuard,
            new LockDateGuard(new LockDateReader(new SettingsRepositoryBuilder().Build())),
            new BranchClock(),
            ctx.LedgerGuard,
            ctx.Coordination,
            ctx.UnitOfWork);
    }

    private static TestContext BuildContext(params TransactionStatus[] statuses)
    {
        return BuildContext(statuses, unitOfWork: null);
    }

    private static TestContext BuildContext(TransactionStatus first, TransactionStatus second, IUnitOfWork unitOfWork)
    {
        return BuildContext([first, second], unitOfWork);
    }

    private static TestContext BuildContext(TransactionStatus[] statuses, IUnitOfWork? unitOfWork)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithUserId(branchUser.UserId).Build();
        var accountId = Guid.NewGuid();
        var date = DateTime.UtcNow.Date;
        var originId = Guid.NewGuid();
        var installments = statuses
            .Select((status, index) => new TransactionBuilder()
                .WithId(index == 0 ? originId : Guid.NewGuid())
                .WithBranchId(branchUser.BranchId)
                .WithAccountId(accountId)
                .WithDate(date)
                .WithDueDate(date.AddDays(30 * (index + 1)))
                .WithValue(100m)
                .WithVersion((uint)(10 + index))
                .WithStatus(status)
                .WithRecordedByOperatorId(callerOperator.Id)
                .WithOriginTransactionId(originId)
                .Build())
            .ToList();

        var transactionsRepository = Substitute.For<ITransactionsRepository>();
        transactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
                originId, branchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(installments);
        transactionsRepository.ListPlanInstallmentsByOriginTransactionIdAndBranchId(
                originId, branchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(installments);

        var memberAccountScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberAccountScopeResolver
            .Resolve(branchUser.UserId, branchUser.BranchId, Arg.Any<CancellationToken>())
            .Returns(new MemberAccountScope(LinkedOperator: callerOperator, AllowedAccountIds: [accountId]));
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        return new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            AccountId = accountId,
            Date = date,
            OriginId = originId,
            Installments = installments,
            AuthenticationService = new AuthenticationServiceBuilder().GetAuthenticatedBranchUser(branchUser).Build(),
            TransactionsRepository = transactionsRepository,
            MemberAccountScopeResolver = memberAccountScopeResolver,
            MutationPermissionGuard = Substitute.For<ITransactionMutationPermissionGuard>(),
            LedgerGuard = SessionTestServices.LedgerGuard(),
            Coordination = coordinationBuilder.Build(),
            CoordinationScope = coordinationBuilder.Scope,
            UnitOfWork = unitOfWork ?? new UnitOfWorkBuilder().Build()
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator CallerOperator { get; init; }
        public required Guid AccountId { get; init; }
        public required DateTime Date { get; init; }
        public required Guid OriginId { get; init; }
        public required List<Transaction> Installments { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IMemberAccountScopeResolver MemberAccountScopeResolver { get; init; }
        public required ITransactionMutationPermissionGuard MutationPermissionGuard { get; init; }
        public required IDailyCloseLedgerGuard LedgerGuard { get; init; }
        public required IDailyCloseAccountCoordination Coordination { get; init; }
        public required IDailyCloseAccountCoordinationScope CoordinationScope { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }

        public RequestFinalizeInstallmentPlanJson ExpectedRequest()
        {
            var builder = new RequestFinalizeInstallmentPlanJsonBuilder().WithInstallments([]);
            foreach (var draft in Installments.Where(installment => installment.Status == TransactionStatus.Draft))
                builder.WithInstallment(draft.Id, draft.Version);
            if (Installments.All(installment => installment.Status != TransactionStatus.Draft))
                builder.WithInstallment(Installments[0].Id, Installments[0].Version);
            return builder.Build();
        }
    }
}
