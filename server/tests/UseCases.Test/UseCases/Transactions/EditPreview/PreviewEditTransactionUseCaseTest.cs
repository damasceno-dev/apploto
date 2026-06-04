using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.EditPreview;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.Transactions.EditPreview;

/// <summary>
/// Edit-preview is the non-persisting twin of Update. These tests verify the same
/// orchestration chain — role check → validate → load → guards → relative validator →
/// build hypothetical edit → project — using the <b>real</b> <see cref="TransactionEditImpactProjector"/>
/// wired through the repository builders, matching the codebase convention for compute
/// helpers (no interface, real collaborator in tests). The projector's three-section math
/// has its own exhaustive coverage in <c>TransactionEditImpactProjectorTest</c>; the
/// overlay construction is covered in <c>HypotheticalTransactionEditTest</c>. Here we only
/// assert enough of the impact — against a single representative Tab/fiado scenario (see
/// <c>BuildContext</c>) — to prove the use case built the edit from snapshot + payload and
/// forwarded the resolved as-of date.
///
/// The "preview never commits" invariant is not asserted here: the use case injects no
/// <see cref="IUnitOfWork"/>, so it holds by construction. On every guard/validation failure we
/// assert the thrown exception — the contract for a rejected preview.
/// </summary>
public class PreviewEditTransactionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnComputedImpactAndEmptyWarnings_WhenManagerEditsActiveTransaction()
    {
        var ctx = BuildContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate);

        response.TransactionId.ShouldBe(ctx.Transaction.Id);
        response.Warnings.ShouldBeEmpty();

        // DueDate overlay + as-of date: due 2025-05-02 vs as-of 2025-06-30 = 59 days
        // (Days31To60); the hypothetical due 2025-06-25 = 5 days (Days0To30).
        response.Impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days31To60);
        response.Impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days0To30);
        response.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        response.Impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();

        // ClientId overlay on a Tab + Out row: R$150 moves off the old client onto the new.
        response.Impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(2);
        var oldDelta = response.Impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == ctx.SnapshotClientId);
        oldDelta.OutstandingDelta.ShouldBe(-150m);
        oldDelta.ClientName.ShouldBe("Maria");
        var newDelta = response.Impact.FiadoBalanceImpact.Deltas.Single(d => d.ClientId == ctx.RequestClientId);
        newDelta.OutstandingDelta.ShouldBe(150m);
        newDelta.ClientName.ShouldBe("João");

        // No daily close seeded → variance section reports an absent day (null status), delta 0.
        response.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        response.Impact.CashVarianceImpact.AccountId.ShouldBeNull();
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
    }

    [Fact]
    public async Task Execute_ShouldLoadSnapshotNoTrackingWithTransactionType_AndNeverTheTrackedVariant()
    {
        var ctx = BuildContext(Role.Admin);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate);

        await ctx.TransactionsRepository.Received(1)
            .GetByIdAndBranchIdAsNoTrackingWithTransactionType(ctx.Transaction.Id, ctx.BranchUser.BranchId);
        await ctx.TransactionsRepository.DidNotReceive()
            .GetByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.TransactionsRepository.DidNotReceive()
            .GetByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>());
    }

    [Fact]
    public async Task Execute_ShouldResolveExplicitQueryAsOfDate_AndNotConsultTheClock()
    {
        var ctx = BuildContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        // As-of 2025-05-10 → due 2025-05-02 is 8 days out = Days0To30, which differs from the
        // clock's branch-local today (2025-06-30 → Days31To60). The distinct bucket proves the
        // explicit date reached the projector, not the clock's.
        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request, new DateTime(2025, 5, 10));

        response.Impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days0To30);
        ctx.BranchClock.DidNotReceive().UtcNow();
        ctx.BranchClock.DidNotReceive().LocalBusinessDate(Arg.Any<DateTime>());
    }

    [Fact]
    public async Task Execute_ShouldFallBackToBranchClockToday_WhenAsOfDateIsOmitted()
    {
        var ctx = BuildContext(Role.Manager);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Transaction.Id, ctx.Request);

        // No explicit date → resolves the clock's branch-local today (2025-06-30) → due
        // 2025-05-02 is 59 days out = Days31To60.
        response.Impact.ReceivableImpact.BucketBefore.ShouldBe(AgingBucket.Days31To60);
        ctx.BranchClock.Received(1).UtcNow();
        ctx.BranchClock.Received(1).LocalBusinessDate(ctx.BranchClockUtcNow);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenCallerIsMember()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var missingId = Guid.NewGuid();
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingWithTransactionTypeReturns(missingId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(missingId, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenTransactionIsCancelled()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.Status = TransactionStatus.Cancelled;
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingWithTransactionTypeReturns(
                ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
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
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoTransactionHasNoClient()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Transaction.TransactionType.RequiresTabAccountAndClient = true;
        ctx.TransactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingWithTransactionTypeReturns(
                ctx.Transaction.Id, ctx.BranchUser.BranchId, ctx.Transaction)
            .Build();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("no client")
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenRequestClientDoesNotExistInBranch()
    {
        var ctx = BuildContext(Role.Manager);
        var missingClientId = Guid.NewGuid();
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("reassign to ghost")
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(null)
            .WithClientId(missingClientId)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();
        ctx.ClientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await ctx.ClientsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(missingClientId, ctx.BranchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenDueDateBeforeTransactionDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("due before date")
            .WithDueDate(ctx.Transaction.Date.AddDays(-1))
            .WithPaidAt(null)
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenPaidAtBeforeTransactionDate()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.Request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("paid before date")
            .WithDueDate(ctx.Transaction.Date.AddDays(1))
            .WithPaidAt(ctx.Transaction.Date.AddDays(-1))
            .WithClientId(null)
            .WithTransactionTime(new TimeOnly(10, 0))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_PAID_AT_BEFORE_DATE);
    }

    [Fact]
    public async Task Execute_ShouldRethrowMutationGuardException()
    {
        var ctx = BuildContext(Role.Manager);
        ctx.MutationPermissionGuard
            .When(guard => guard.EnsureAllowed(
                Arg.Any<Transaction>(),
                Arg.Any<Role>(),
                Arg.Any<Operator?>(),
                Arg.Any<DateTime>()))
            .Do(_ => throw new ConflictException(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY));
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.Transaction.Id, ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        ctx.MutationPermissionGuard.Received(1).EnsureAllowed(
            ctx.Transaction,
            Role.Manager,
            null,
            Arg.Any<DateTime>());
    }

    private static PreviewEditTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);
        var projector = new TransactionEditImpactProjector(
            ctx.DailyClosesRepository,
            ctx.ClientsRepository,
            Substitute.For<ICashVarianceCalculator>(),
            Substitute.For<ICashVarianceProductResolver>());

        return new PreviewEditTransactionUseCase(
            ctx.AuthenticationService,
            ctx.TransactionsRepository,
            ctx.ClientsRepository,
            ctx.MutationPermissionGuard,
            lockDateGuard,
            projector,
            ctx.BranchClock);
    }

    /// <summary>
    /// Shared base fixture for every test here. Defaults to one representative scenario: a
    /// Manager/Admin editing an unpaid Tab (fiado) row — reassigning the client and shifting the
    /// due date — so the happy-path test can assert real <c>ReceivableImpact</c> (bucket shift) and
    /// <c>FiadoBalanceImpact</c> (client move) output from the real projector. The Tab/fiado defaults
    /// are incidental to the failure-path tests, which reuse this context and override
    /// <c>ctx.Request</c> / <c>ctx.Transaction</c> / the repositories as their scenario needs. The
    /// per-status / per-direction / Tab-vs-not impact matrix is not exercised here — that lives in
    /// <c>TransactionEditImpactProjectorTest</c>.
    /// </summary>
    private static TestContext BuildContext(Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var branch = new BranchBuilder().WithId(branchUser.BranchId).Build();
        var operatorContext = new OperatorBuilder().WithBranch(branch).Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Fiado",
            DefaultDirection = Direction.Out,
            BranchId = branch.Id,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .WithRequiresTabAccountAndClient(false)
            .Build();
        var tabAccount = new AccountBuilder()
            .WithType(AccountType.Tab)
            .WithBranch(branch)
            .Build();

        var transactionDate = new DateTime(2025, 5, 1);
        var snapshotClientId = Guid.NewGuid();
        var requestClientId = Guid.NewGuid();

        var transaction = new TransactionBuilder()
            .WithBranch(branch)
            .WithAccount(tabAccount)
            .WithTransactionType(transactionType)
            .WithValue(150m)
            .WithDate(transactionDate)
            .WithDueDate(new DateTime(2025, 5, 2))
            .WithPaidAt(null)
            .WithDescription("snapshot description")
            .WithClientId(snapshotClientId)
            .WithTransactionTime(new TimeOnly(9, 15))
            .WithRecordedByOperatorId(operatorContext.Id)
            .Build();

        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription("hypothetical description")
            .WithDueDate(new DateTime(2025, 6, 25))
            .WithPaidAt(null)
            .WithClientId(requestClientId)
            .WithTransactionTime(new TimeOnly(16, 30))
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder()
            .GetByIdAndBranchIdAsNoTrackingWithTransactionTypeReturns(transaction.Id, branchUser.BranchId, transaction)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(
                requestClientId,
                branchUser.BranchId,
                new ClientBuilder().WithId(requestClientId).WithBranchId(branchUser.BranchId).WithName("João").Build())
            .GetActiveByIdAndBranchIdAsNoTracking(
                snapshotClientId,
                branchUser.BranchId,
                new ClientBuilder().WithId(snapshotClientId).WithBranchId(branchUser.BranchId).WithName("Maria").Build())
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder().Build();
        var dailyClosesRepository = new DailyClosesRepositoryBuilder().Build();
        var mutationPermissionGuard = Substitute.For<ITransactionMutationPermissionGuard>();

        var branchClockUtcNow = new DateTime(2025, 6, 30, 12, 0, 0, DateTimeKind.Utc);
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(branchClockUtcNow);
        branchClock.LocalBusinessDate(branchClockUtcNow).Returns(new DateTime(2025, 6, 30));

        return new TestContext
        {
            BranchUser = branchUser,
            Transaction = transaction,
            Request = request,
            SnapshotClientId = snapshotClientId,
            RequestClientId = requestClientId,
            AsOfDate = new DateTime(2025, 6, 30),
            AuthenticationService = authenticationService,
            TransactionsRepository = transactionsRepository,
            ClientsRepository = clientsRepository,
            SettingsRepository = settingsRepository,
            DailyClosesRepository = dailyClosesRepository,
            MutationPermissionGuard = mutationPermissionGuard,
            BranchClock = branchClock,
            BranchClockUtcNow = branchClockUtcNow
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Transaction Transaction { get; set; }
        public required RequestUpdateTransactionJson Request { get; set; }
        public required Guid SnapshotClientId { get; init; }
        public required Guid RequestClientId { get; init; }
        public required DateTime AsOfDate { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required ITransactionMutationPermissionGuard MutationPermissionGuard { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required DateTime BranchClockUtcNow { get; init; }
    }
}
