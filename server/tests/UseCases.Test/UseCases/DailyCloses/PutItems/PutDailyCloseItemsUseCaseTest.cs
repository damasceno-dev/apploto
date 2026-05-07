using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.DailyCloses.PutItems;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.DailyCloses.PutItems;

public class PutDailyCloseItemsUseCaseTest
{
    // ──────────────────────────────────────────────
    // Happy paths
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldInsertItemsStampAuditAndDelegateToGuard_WhenDraftAndManager()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        // Guard delegation with exact args
        ctx.WorkflowGuard.Received(1).EnsureCanEditItems(ctx.DailyClose, ctx.BranchUser, ctx.CallerOperator);

        // Audit stamped from the single captured clock instant
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        // New item delegated to the repository so EF Core issues INSERT (not UPDATE)
        await ctx.DailyCloseItemsRepository.Received(1).Add(
            Arg.Is<DailyCloseItem>(i =>
                i.ProductId == ctx.Product.Id &&
                i.Value == 100m &&
                i.DailyCloseId == ctx.DailyClose.Id));

        // Status unchanged for Draft outcome
        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Draft);

        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateExistingActiveItem_WhenProductIdAlreadyPresentOnClose()
    {
        var ctx = BuildHappyPathContext(Role.Manager);

        // Pre-seed an existing active item for the same product
        var existingItem = new DailyCloseItemBuilder()
            .WithProductId(ctx.Product.Id)
            .WithValue(50m)
            .Build();

        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Draft)
            .WithAccount(ctx.DailyClose.Account)
            .WithItems([existingItem])
            .Build();
        RewireDailyCloseRepository(ctx);

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        // Value updated in-place; no duplicate inserted
        existingItem.Value.ShouldBe(100m);
        ctx.DailyClose.Items.Count(i => i.Active && i.ProductId == ctx.Product.Id).ShouldBe(1);

        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldInsertUpdateSoftDeleteItems_AndPreserveCashVarianceRow()
    {
        var ctx = BuildHappyPathContext(Role.Manager);

        var productB = new ProductBuilder().WithBranchId(ctx.BranchUser.BranchId).Build();
        var productC = new ProductBuilder().WithBranchId(ctx.BranchUser.BranchId).Build();

        // Pre-existing items: A (omitted from payload → soft-delete),
        //                     B (in payload → update),
        //                     CashVariance (omitted → must be preserved)
        var itemA = new DailyCloseItemBuilder()
            .WithProductId(Guid.NewGuid())
            .WithValue(10m)
            .Build();
        var itemB = new DailyCloseItemBuilder()
            .WithProductId(productB.Id)
            .WithValue(20m)
            .Build();
        var itemCV = new DailyCloseItemBuilder()
            .WithProductId(ctx.CashVarianceProductId)
            .WithValue(5m)
            .Build();

        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Draft)
            .WithAccount(ctx.DailyClose.Account)
            .WithItems([itemA, itemB, itemCV])
            .Build();
        RewireDailyCloseRepository(ctx);

        // Payload: B (update) + C (insert) — A and CashVariance omitted
        ctx.Request = new RequestPutDailyCloseItemsJson
        {
            Items =
            [
                new RequestUpsertDailyCloseItemJson { ProductId = productB.Id, Value = 99m },
                new RequestUpsertDailyCloseItemJson { ProductId = productC.Id, Value = 55m }
            ]
        };
        ctx.ProductsRepository = new ProductsRepositoryBuilder()
            .ListActiveByIdsAndBranchIdAsNoTrackingReturns(
                [productB.Id, productC.Id],
                ctx.BranchUser.BranchId,
                [productB, productC])
            .Build();

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        // A was omitted → soft-deleted
        itemA.Active.ShouldBeFalse();

        // B was in payload → value updated, still active
        itemB.Active.ShouldBeTrue();
        itemB.Value.ShouldBe(99m);

        // CashVariance was omitted but must never be deactivated
        itemCV.Active.ShouldBeTrue();

        // C was new → Add was called on the repository so EF Core issues INSERT
        await ctx.DailyCloseItemsRepository.Received(1).Add(
            Arg.Is<DailyCloseItem>(i =>
                i.ProductId == productC.Id &&
                i.Value == 55m &&
                i.DailyCloseId == ctx.DailyClose.Id));

        // B was updated (existing row) → Add was NOT called for it
        await ctx.DailyCloseItemsRepository.DidNotReceive().Add(
            Arg.Is<DailyCloseItem>(i => i.ProductId == productB.Id));

        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAutoTransitionRejectedToDraft_AndStampAuditFromSingleInstant()
    {
        var ctx = BuildHappyPathContext(Role.Manager);

        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Rejected)
            .WithAccount(ctx.DailyClose.Account)
            .Build();
        RewireDailyCloseRepository(ctx);
        ctx.WorkflowGuard
            .EnsureCanEditItems(Arg.Any<DailyClose>(), Arg.Any<BranchUser>(), Arg.Any<Operator?>())
            .Returns(DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft);

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Draft);
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldRecallSubmittedToDraftAndClearSubmittedAt_WhenRecordingOperatorMemberOnSameDay()
    {
        var ctx = BuildHappyPathContext(Role.Member);

        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Submitted)
            .WithSubmittedAt(DateTime.UtcNow.AddHours(-1))
            .WithAccount(ctx.DailyClose.Account)
            .Build();
        RewireDailyCloseRepository(ctx);
        ctx.WorkflowGuard
            .EnsureCanEditItems(Arg.Any<DailyClose>(), Arg.Any<BranchUser>(), Arg.Any<Operator?>())
            .Returns(DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft);

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Draft);
        ctx.DailyClose.SubmittedAt.ShouldBeNull();
        ctx.DailyClose.UpdatedAt.ShouldBe(ctx.Now);
        ctx.DailyClose.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldRecallSubmittedToDraft_WhenManager()
    {
        var ctx = BuildHappyPathContext(Role.Manager);

        ctx.DailyClose = new DailyCloseBuilder()
            .WithId(ctx.DailyClose.Id)
            .WithStatus(DailyCloseStatus.Submitted)
            .WithSubmittedAt(DateTime.UtcNow.AddHours(-2))
            .WithAccount(ctx.DailyClose.Account)
            .Build();
        RewireDailyCloseRepository(ctx);
        ctx.WorkflowGuard
            .EnsureCanEditItems(Arg.Any<DailyClose>(), Arg.Any<BranchUser>(), Arg.Any<Operator?>())
            .Returns(DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft);

        var useCase = CreateUseCase(ctx);
        await useCase.Execute(ctx.DailyClose.Id, ctx.Request);

        ctx.DailyClose.Status.ShouldBe(DailyCloseStatus.Draft);
        ctx.DailyClose.SubmittedAt.ShouldBeNull();
        await ctx.UnitOfWork.Received(1).Commit();
    }

    // ──────────────────────────────────────────────
    // Failure paths
    // ──────────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenWorkflowGuardDeniesEdit()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        ctx.WorkflowGuard
            .EnsureCanEditItems(Arg.Any<DailyClose>(), Arg.Any<BranchUser>(), Arg.Any<Operator?>())
            .Throws(new ConflictException(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE));

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenDailyCloseCrossBranchOrMissing()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenProductCrossBranchOrInactive()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        // Return empty — count(0) != distinctPayloadIds(1) → NotFoundException
        ctx.ProductsRepository = new ProductsRepositoryBuilder()
            .ListActiveByIdsAndBranchIdAsNoTrackingReturns(
                [ctx.Product.Id],
                ctx.BranchUser.BranchId,
                [])
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Member);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(
                ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder().Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberIsOutOfScope()
    {
        var ctx = BuildHappyPathContext(Role.Member);
        // Operator exists but holds no accounts → scope excludes close.AccountId
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator.Id, [])
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksCloseDate()
    {
        var ctx = BuildHappyPathContext(Role.Manager);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = ctx.DailyClose.Date  // close date <= lock date → locked
                })
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenPayloadReferencesCashVarianceProduct()
    {
        var ctx = BuildHappyPathContext(Role.Manager);

        // Payload contains the system-managed CashVariance product id
        ctx.Request = new RequestPutDailyCloseItemsJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.CashVarianceProductId, Value = 10m }]
        };

        // Products repo resolves it to pass the count check before the forbidden check
        var cvProduct = new ProductBuilder()
            .WithId(ctx.CashVarianceProductId)
            .WithBranchId(ctx.BranchUser.BranchId)
            .Build();
        ctx.ProductsRepository = new ProductsRepositoryBuilder()
            .ListActiveByIdsAndBranchIdAsNoTrackingReturns(
                [ctx.CashVarianceProductId],
                ctx.BranchUser.BranchId,
                [cvProduct])
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() =>
            useCase.Execute(ctx.DailyClose.Id, ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static PutDailyCloseItemsUseCase CreateUseCase(HappyPathContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);

        return new PutDailyCloseItemsUseCase(
            ctx.AuthenticationService,
            ctx.DailyClosesRepository,
            ctx.DailyCloseItemsRepository,
            ctx.ProductsRepository,
            memberAccountScopeResolver,
            memberAccountScopeGuard,
            ctx.WorkflowGuard,
            ctx.CashVarianceProductResolver,
            lockDateGuard,
            new FixedBranchClock(ctx.Now),
            ctx.UnitOfWork);
    }

    /// <summary>
    /// Rebuilds ctx.DailyClosesRepository after ctx.DailyClose is replaced,
    /// so the repo returns the new close instance on GetByIdAndBranchId.
    /// </summary>
    private static void RewireDailyCloseRepository(HappyPathContext ctx)
    {
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(ctx.DailyClose.Id, ctx.BranchUser.BranchId, ctx.DailyClose)
            .Build();
    }

    private static HappyPathContext BuildHappyPathContext(Role role)
    {
        var now = new DateTime(2026, 4, 29, 12, 0, 0, DateTimeKind.Utc);

        var branchUser = new BranchUserBuilder().WithRole(role).Build();

        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();

        var account = new AccountBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();

        var product = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .Build();

        var cashVarianceProductId = Guid.NewGuid();

        var close = new DailyCloseBuilder()
            .WithStatus(DailyCloseStatus.Draft)
            .WithAccount(account)
            .WithDate(DateTime.Today)
            .Build();

        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(account)
            .Build();

        var request = new RequestPutDailyCloseItemsJson
        {
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 100m }]
        };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();

        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(
                branchUser.UserId, branchUser.BranchId, callerOperator)
            .Build();

        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();

        var dailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(close.Id, branchUser.BranchId, close)
            .Build();

        var dailyCloseItemsRepository = new DailyCloseItemsRepositoryBuilder().Build();

        var productsRepository = new ProductsRepositoryBuilder()
            .ListActiveByIdsAndBranchIdAsNoTrackingReturns(
                [product.Id], branchUser.BranchId, [product])
            .Build();

        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();

        var workflowGuard = Substitute.For<IDailyCloseWorkflowGuard>();
        workflowGuard
            .EnsureCanEditItems(Arg.Any<DailyClose>(), Arg.Any<BranchUser>(), Arg.Any<Operator?>())
            .Returns(DailyCloseEditItemsOutcome.EditOnDraft);

        var cashVarianceProductResolver = Substitute.For<ICashVarianceProductResolver>();
        cashVarianceProductResolver
            .GetIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(cashVarianceProductId);

        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new HappyPathContext
        {
            Now = now,
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            DailyClose = close,
            Product = product,
            CashVarianceProductId = cashVarianceProductId,
            Request = request,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            DailyClosesRepository = dailyClosesRepository,
            DailyCloseItemsRepository = dailyCloseItemsRepository,
            ProductsRepository = productsRepository,
            SettingsRepository = settingsRepository,
            WorkflowGuard = workflowGuard,
            CashVarianceProductResolver = cashVarianceProductResolver,
            UnitOfWork = unitOfWork
        };
    }

    private class HappyPathContext
    {
        public required DateTime Now { get; init; }
        public required BranchUser BranchUser { get; set; }
        public required Operator CallerOperator { get; set; }
        public required DailyClose DailyClose { get; set; }
        public required Product Product { get; set; }
        public required Guid CashVarianceProductId { get; init; }
        public required RequestPutDailyCloseItemsJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required IDailyCloseItemsRepository DailyCloseItemsRepository { get; set; }
        public required IProductsRepository ProductsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyCloseWorkflowGuard WorkflowGuard { get; set; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; set; }
        public required IUnitOfWork UnitOfWork { get; set; }
    }

    /// <summary>Clock that always returns the same fixed UTC instant.</summary>
    private sealed class FixedBranchClock(DateTime utcNow) : IBranchClock
    {
        public DateTime UtcNow() => utcNow;
        public DateTime LocalBusinessDateTime(DateTime utcInstant) => utcInstant;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }
}
