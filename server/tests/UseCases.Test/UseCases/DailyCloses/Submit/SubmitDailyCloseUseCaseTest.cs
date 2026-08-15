using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.Submit;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.DailyCloses.Submit;

public class SubmitDailyCloseUseCaseTest
{
    public static TheoryData<DailyCloseStatus> NotSubmittableStatuses =>
    [
        DailyCloseStatus.Submitted,
        DailyCloseStatus.Approved
    ];

    [Fact]
    public async Task Execute_ShouldSubmitDraftAndAddCashVarianceItem_WhenNoActiveCashVarianceExists()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.DailyClose.Id);

        response.Status.ShouldBe(DailyCloseStatus.Submitted);
        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Submitted);
        ctx.DailyClose.SubmittedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.SubmittedAt.ShouldBe(ctx.DailyClose.UpdatedAt);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.DailyClose.SubmittedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.DailyClose.SubmittedByOperatorId.ShouldBe(ctx.CallerOperator!.Id);

        await ctx.DailyCloseItemsRepository.Received(1).Add(
            Arg.Is<DailyCloseItem>(item =>
                item.ProductId == ctx.CashVarianceProductId &&
                item.Active &&
                item.Value == ctx.ComputedVariance &&
                item.DailyCloseId == ctx.DailyClose.Id),
            Arg.Any<CancellationToken>());

        ctx.WorkflowGuard.Received(1)
            .EnsureCanSubmit(ctx.DailyClose, ctx.BranchUser, ctx.CallerOperator);
        await ctx.CashVarianceCalculator.Received(1).CalculateWithOpeningSourceAsync(
            Arg.Is<Guid>(id => id == ctx.BranchUser.BranchId),
            Arg.Is<Guid>(id => id == ctx.DailyClose.AccountId),
            Arg.Is<DateTime>(date => date == ctx.DailyClose.Date),
            Arg.Is<Guid>(id => id == ctx.DailyClose.Id),
            Arg.Is<Guid>(id => id == ctx.CashVarianceProductId),
            Arg.Any<DailyClose?>(),
            Arg.Any<CancellationToken>());
        await ctx.DailyCloseLedgerGuard.Received(1).EnsureNoOutstandingDraftTransactions(
            ctx.BranchUser.BranchId,
            ctx.DailyClose.AccountId,
            ctx.DailyClose.Date.Date,
            Arg.Any<CancellationToken>());
        await ctx.DailyCloseLedgerCoordination.Received(1).Acquire(
            ctx.BranchUser.BranchId,
            ctx.DailyClose.AccountId,
            Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).Commit();
        await ctx.DailyCloseLedgerCoordinationScope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldMutateExistingCashVarianceItemInPlace_WhenCloseIsRejected()
    {
        var ctx = BuildContext(Role.Manager, status: DailyCloseStatus.Rejected, linkedOperator: false);
        var existingItemId = Guid.NewGuid();
        var existingCashVariance = new DailyCloseItemBuilder()
            .WithId(existingItemId)
            .WithDailyCloseId(ctx.DailyClose.Id)
            .WithProductId(ctx.CashVarianceProductId)
            .WithValue(12m)
            .Build();
        ctx.DailyClose = CloneClose(ctx.DailyClose, status: DailyCloseStatus.Rejected, items: [existingCashVariance]);
        ctx.DailyClose.RejectionReason = "Corrija o fechamento";
        RewireDailyCloseRepository(ctx);

        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.DailyClose.Id);

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Submitted);
        ctx.DailyClose.RejectionReason.ShouldBeNull();
        ctx.DailyClose.Items.Count(item => item.ProductId == ctx.CashVarianceProductId && item.Active).ShouldBe(1);
        existingCashVariance.Id.ShouldBe(existingItemId);
        existingCashVariance.Value.ShouldBe(ctx.ComputedVariance);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldStampSubmitterWithoutChangingRecorder_WhenManagerSubmitsMemberCount()
    {
        var ctx = BuildContext(Role.Manager);
        var recordingUser = new UserBuilder().Build();
        var recordingOperator = new OperatorBuilder()
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithUser(recordingUser)
            .Build();
        ctx.DailyClose.RecordedByUserId = recordingUser.Id;
        ctx.DailyClose.RecordedByUser = recordingUser;
        ctx.DailyClose.RecordedByOperatorId = recordingOperator.Id;
        ctx.DailyClose.RecordedByOperator = recordingOperator;
        RewireDailyCloseRepository(ctx);

        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.DailyClose.Id);

        ctx.DailyClose.RecordedByUserId.ShouldBe(recordingUser.Id);
        ctx.DailyClose.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        ctx.DailyClose.SubmittedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.DailyClose.SubmittedByOperatorId.ShouldBe(ctx.CallerOperator!.Id);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenDailyCloseIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenRequiresOperatorLink_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: false);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        ctx.WorkflowGuard.DidNotReceiveWithAnyArgs()
            .EnsureCanSubmit(null!, null!, null);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowForbiddenOutOfScope_WhenMemberLinkedOperatorCannotAccessAccount()
    {
        var ctx = BuildContext(Role.Member, linkedAccount: false);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        ctx.WorkflowGuard.DidNotReceiveWithAnyArgs()
            .EnsureCanSubmit(null!, null!, null);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [MemberData(nameof(NotSubmittableStatuses))]
    public async Task Execute_ShouldThrowConflict_WhenCloseIsNotSubmittable(DailyCloseStatus status)
    {
        var ctx = BuildContext(Role.Manager, status: status, linkedOperator: false);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksCloseDate()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.DailyClose.Date
                })
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        await ctx.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateAsync(Guid.Empty, Guid.Empty, default, Guid.Empty, Guid.Empty, CancellationToken.None);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRejectOutstandingDraftTransactions_BeforeVarianceCalculation()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.DailyCloseLedgerGuard
            .EnsureNoOutstandingDraftTransactions(
                ctx.BranchUser.BranchId,
                ctx.DailyClose.AccountId,
                ctx.DailyClose.Date.Date,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new ConflictException(
                ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS)));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);
        await ctx.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateAsync(Guid.Empty, Guid.Empty, default, Guid.Empty, Guid.Empty, CancellationToken.None);
        await ctx.UnitOfWork.DidNotReceive().Commit();
        await ctx.DailyCloseLedgerCoordinationScope.DidNotReceive()
            .Complete(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenWorkflowGuardRejectsMemberOlderDay()
    {
        var ctx = BuildContext(Role.Member);
        ctx.DailyClose = CloneClose(
            ctx.DailyClose,
            date: ctx.Today.AddDays(-1),
            submittedByOperator: ctx.CallerOperator,
            status: DailyCloseStatus.Draft);
        RewireDailyCloseRepository(ctx);
        ctx.WorkflowGuard = new DailyCloseWorkflowGuard(ctx.BranchClock);

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        await ctx.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateAsync(Guid.Empty, Guid.Empty, default, Guid.Empty, Guid.Empty, CancellationToken.None);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldRejectCloseWithoutSuccessfulItemSave()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.DailyClose = CloneClose(ctx.DailyClose, clearItemsFirstRecordedAt: true);
        RewireDailyCloseRepository(ctx);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.DailyClose.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_ITEMS_NOT_RECORDED);
        await ctx.DailyCloseLedgerGuard.DidNotReceiveWithAnyArgs()
            .EnsureNoOutstandingDraftTransactions(Guid.Empty, Guid.Empty, default, CancellationToken.None);
        await ctx.CashVarianceCalculator.DidNotReceiveWithAnyArgs()
            .CalculateWithOpeningSourceAsync(
                Guid.Empty,
                Guid.Empty,
                default,
                Guid.Empty,
                Guid.Empty,
                null,
                CancellationToken.None);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldResolveOpeningSourceAndLockDateExactlyOnceAndReuseSource()
    {
        var ctx = BuildContext(Role.Manager);
        var openingSource = new DailyCloseBuilder()
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithAccountId(ctx.DailyClose.AccountId)
            .WithDate(ctx.DailyClose.Date.AddDays(-1))
            .WithItemsFirstRecordedAt(ctx.Now.AddDays(-1))
            .Build();
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, ctx.DailyClose)
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                ctx.DailyClose.AccountId,
                ctx.DailyClose.Date,
                openingSource)
            .Build();
        var setting = new Setting
        {
            BranchId = ctx.BranchUser.BranchId,
            LockDate = ctx.DailyClose.Date.AddDays(-10)
        };
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(ctx.BranchUser.BranchId, setting)
            .Build();
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.DailyClose.Id);

        await ctx.DailyClosesRepository.Received(1)
            .GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
                ctx.BranchUser.BranchId,
                ctx.DailyClose.AccountId,
                ctx.DailyClose.Date,
                Arg.Any<CancellationToken>());
        await ctx.SettingsRepository.Received(1)
            .GetByBranchIdAsNoTracking(ctx.BranchUser.BranchId, Arg.Any<CancellationToken>());
        await ctx.CashVarianceCalculator.Received(1)
            .CalculateWithOpeningSourceAsync(
                ctx.BranchUser.BranchId,
                ctx.DailyClose.AccountId,
                ctx.DailyClose.Date,
                ctx.DailyClose.Id,
                ctx.CashVarianceProductId,
                openingSource,
                Arg.Any<CancellationToken>());
    }

    private static SubmitDailyCloseUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(new LockDateReader(ctx.SettingsRepository));

        return new SubmitDailyCloseUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.DailyCloseItemsRepository,
            ctx.TransactionsRepository,
            memberAccountScopeResolver,
            memberAccountScopeGuard,
            ctx.WorkflowGuard,
            new LockDateReader(ctx.SettingsRepository),
            lockDateGuard,
            ctx.CashVarianceProductResolver,
            ctx.CashVarianceCalculator,
            ctx.BranchClock,
            ctx.DailyCloseLedgerGuard,
            ctx.DailyCloseLedgerCoordination,
            ctx.UnitOfWork);
    }

    private static TestContext BuildContext(
        Role role,
        DailyCloseStatus status = DailyCloseStatus.Draft,
        bool linkedOperator = true,
        bool linkedAccount = true)
    {
        var now = new DateTime(2026, 4, 30, 14, 30, 0, DateTimeKind.Utc);
        var today = now.Date;
        var branchUser = new BranchUserBuilder()
            .WithRole(role)
            .Build();
        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();
        var callerOperator = linkedOperator
            ? new OperatorBuilder()
                .WithBranchId(branchUser.BranchId)
                .WithUserId(branchUser.UserId)
                .Build()
            : null;
        var close = new DailyCloseBuilder()
            .WithStatus(status)
            .WithAccount(account)
            .WithDate(today)
            .WithRecordedBy(branchUser.User, callerOperator)
            .WithItemsFirstRecordedAt(now.AddMinutes(-1))
            .Build();
        var cashVarianceProductId = Guid.NewGuid();
        const decimal computedVariance = 123.45m;

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(
                branchUser.UserId,
                branchUser.BranchId,
                callerOperator)
            .Build();
        IReadOnlyList<OperatorAccount> operatorAccounts = callerOperator is not null && linkedAccount
            ? [new OperatorAccountBuilder().WithOperator(callerOperator).WithAccount(account).Build()]
            : [];
        var operatorAccountsRepository = callerOperator is not null
            ? new OperatorAccountsRepositoryBuilder()
                .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, operatorAccounts)
                .Build()
            : new OperatorAccountsRepositoryBuilder().Build();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(close.Id, branchUser.BranchId, close)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();
        var workflowGuard = Substitute.For<IDailyCloseWorkflowGuard>();
        var cashVarianceProductResolver = Substitute.For<ICashVarianceProductResolver>();
        cashVarianceProductResolver
            .GetIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(cashVarianceProductId);
        var cashVarianceCalculator = Substitute.For<ICashVarianceCalculator>();
        cashVarianceCalculator
            .CalculateWithOpeningSourceAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DateTime>(),
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<DailyClose?>(),
                Arg.Any<CancellationToken>())
            .Returns(computedVariance);
        var dailyCloseLedgerGuard = Substitute.For<IDailyCloseLedgerGuard>();
        var dailyCloseItemsRepository = new DailyCloseItemsRepositoryBuilder().Build();
        var transactionsRepository = new TransactionsRepositoryBuilder().Build();
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();

        return new TestContext
        {
            Now = now,
            Today = today,
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            DailyClose = close,
            CashVarianceProductId = cashVarianceProductId,
            ComputedVariance = computedVariance,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            DailyClosesRepository = dailyClosesRepository,
            DailyCloseItemsRepository = dailyCloseItemsRepository,
            TransactionsRepository = transactionsRepository,
            SettingsRepository = settingsRepository,
            WorkflowGuard = workflowGuard,
            CashVarianceProductResolver = cashVarianceProductResolver,
            CashVarianceCalculator = cashVarianceCalculator,
            BranchClock = new FixedBranchClock(now),
            DailyCloseLedgerGuard = dailyCloseLedgerGuard,
            DailyCloseLedgerCoordination = coordinationBuilder.Build(),
            DailyCloseLedgerCoordinationScope = coordinationBuilder.Scope,
            UnitOfWork = new UnitOfWorkBuilder().Build()
        };
    }

    private static void RewireDailyCloseRepository(TestContext ctx)
    {
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, ctx.DailyClose)
            .Build();
    }

    private static DailyClose CloneClose(
        DailyClose source,
        DailyCloseStatus? status = null,
        DateTime? date = null,
        Operator? submittedByOperator = null,
        IReadOnlyList<DailyCloseItem>? items = null,
        bool clearItemsFirstRecordedAt = false)
    {
        var builder = new DailyCloseBuilder()
            .WithId(source.Id)
            .WithCreatedAt(source.CreatedAt)
            .WithStatus(status ?? source.Status)
            .WithAccount(source.Account)
            .WithDate(date ?? source.Date)
            .WithOpenedByUser(source.OpenedByUser)
            .WithSubmittedBy(source.SubmittedByUser, submittedByOperator ?? source.SubmittedByOperator)
            .WithItemsFirstRecordedAt(clearItemsFirstRecordedAt ? null : source.ItemsFirstRecordedAt)
            .WithItems(items ?? source.Items.ToList());

        if (clearItemsFirstRecordedAt is false && source.RecordedByUser is not null)
            builder.WithRecordedBy(source.RecordedByUser, source.RecordedByOperator);

        return builder.Build();
    }

    private sealed class FixedBranchClock(DateTime now) : IBranchClock
    {
        public DateTime UtcNow() => now;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => utcInstant;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }

    private sealed class TestContext
    {
        public required DateTime Now { get; init; }
        public required DateTime Today { get; init; }
        public required BranchUser BranchUser { get; init; }
        public required Operator? CallerOperator { get; init; }
        public required DailyClose DailyClose { get; set; }
        public required Guid CashVarianceProductId { get; init; }
        public required decimal ComputedVariance { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; init; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required IDailyCloseItemsRepository DailyCloseItemsRepository { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
        public required ICashVarianceCalculator CashVarianceCalculator { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IDailyCloseLedgerGuard DailyCloseLedgerGuard { get; init; }
        public required IDailyCloseAccountCoordination DailyCloseLedgerCoordination { get; init; }
        public required IDailyCloseAccountCoordinationScope DailyCloseLedgerCoordinationScope { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
