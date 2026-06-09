using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Holidays;
using server.Application.Services.Members;
using server.Application.Services.Reports;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.CreatePreview;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.CreatePreview;

/// <summary>
/// Create-impact preview is the non-persisting twin of <c>CreateTransactionUseCase</c>. These tests
/// run the orchestration chain — authenticate → validate → resolve the shared
/// <see cref="TransactionCreatePreamble"/> context → build the hypothetical create → project — using
/// the <b>real</b> <see cref="TransactionEditImpactProjector"/> wired through the repository builders
/// (no interface to substitute, matching the codebase convention for compute helpers).
///
/// The headline is the inverse of the edit twin: a create sets <c>Value</c>/<c>Direction</c>/
/// <c>Date</c> fresh, so the cash-variance <c>VarianceDelta</c> is genuinely non-zero. Member scope
/// mirrors <c>CreateTransactionUseCaseTest</c> exactly — the preamble surfaces the same errors the
/// write flow throws. Note the no-linked-operator case is an <see cref="OnValidationException"/>
/// (the <c>RecordedByOperator</c> resolver runs before the account-scope guard), not a 403; that is
/// the real create-flow behavior and preview/write parity demands it.
///
/// The "preview never commits" invariant is not asserted here: the use case injects no
/// <see cref="IUnitOfWork"/> or <see cref="ITransactionsRepository"/>, so it holds by construction.
/// </summary>
public class PreviewCreateTransactionUseCaseTest
{
    private static readonly DateTime TransactionDate = new(2025, 5, 1);

    [Fact]
    public async Task Execute_ShouldReturnNonZeroVarianceReceivableAndFiado_WhenManagerPreviewsActiveTabCreate()
    {
        // Headline: an Active Tab/Out create for R$200 due same-day, previewed at as-of 2025-05-15.
        var ctx = BuildContext(Role.Manager, AccountType.Tab, Direction.Out, value: 200m, withClient: true);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Warnings.ShouldBeEmpty();

        // Cash variance: the new row adds its net flow (Out = −200) to (In − Out), so the variance
        // moves by the negative of that → +200. Genuinely non-zero — the whole point of this phase.
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(200m);
        // No daily close exists for the Tab account/date → no current/projected variance surfaced.
        response.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        response.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        response.Impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();

        // Receivable: an unpaid Tab row appears; due 2025-05-01 vs as-of 2025-05-15 = 14 days → Days0To30.
        response.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeTrue();
        response.Impact.ReceivableImpact.RowDisappearsFromOpenReceivables.ShouldBeFalse();
        response.Impact.ReceivableImpact.BucketBefore.ShouldBeNull();
        response.Impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days0To30);

        // Fiado: §6.4 Out raises outstanding (+) — a single +200 delta lands on the selected client.
        response.Impact.FiadoBalanceImpact.Deltas.Count.ShouldBe(1);
        var delta = response.Impact.FiadoBalanceImpact.Deltas.Single();
        delta.ClientId.ShouldBe(ctx.ClientId!.Value);
        delta.ClientName.ShouldBe("Headline Client");
        delta.OutstandingDelta.ShouldBe(200m);
    }

    [Fact]
    public async Task Execute_ShouldUseInDirectionSign_ForFiadoAndVariance()
    {
        // In lowers outstanding (§6.4 signedValue = −Value); variance moves by −NetFlow(In) = −Value.
        var ctx = BuildContext(Role.Manager, AccountType.Tab, Direction.In, value: 120m, withClient: true);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Impact.FiadoBalanceImpact.Deltas.Single().OutstandingDelta.ShouldBe(-120m);
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-120m);
    }

    [Fact]
    public async Task Execute_ShouldLeaveAllSectionsEmpty_WhenSaveAsDraftIsTrue()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Tab, Direction.Out, value: 200m, withClient: true);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(TransactionDate)
            .WithValue(200m)
            .WithTransactionTypeId(ctx.TransactionType.Id)
            .WithAccountId(ctx.Account.Id)
            .WithClientId(ctx.ClientId)
            .WithSaveAsDraft(true)
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        // A Draft would-be row hits none of the Active-filtered surfaces → every section empty/zero.
        response.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        response.Impact.ReceivableImpact.BucketAfter.ShouldBeNull();
        response.Impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(0m);
        response.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBeNull();
        response.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
    }

    [Fact]
    public async Task Execute_ShouldLeaveReceivableAndFiadoEmpty_WhenNonTabAccount()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Impact.ReceivableImpact.RowAppearsInOpenReceivables.ShouldBeFalse();
        response.Impact.ReceivableImpact.BucketAfter.ShouldBeNull();
        response.Impact.FiadoBalanceImpact.Deltas.ShouldBeEmpty();
        // Variance delta is computed for every account type.
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-100m);
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task Execute_ShouldLiveRecomputeCurrentVariance_WhenCloseIsNonDraft(DailyCloseStatus status)
    {
        // Submitted/Approved/Rejected all retain a complete submitted closing-count set, so §6.12
        // yields a real number for each (matching the edit twin's non-Draft rule).
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        var (productId, close) = SeedClose(ctx, status, currentVariance: 425m);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(status);
        response.Impact.CashVarianceImpact.AccountId.ShouldBe(ctx.Account.Id);
        response.Impact.CashVarianceImpact.Date.ShouldBe(TransactionDate);
        response.Impact.CashVarianceImpact.CurrentVariance.ShouldBe(425m);
        // VarianceDelta = −NetFlow(In, 100) = −100 → projected = 425 + (−100) = 325.
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-100m);
        response.Impact.CashVarianceImpact.ProjectedVariance.ShouldBe(325m);
        await ctx.CashVarianceCalculator.Received(1).CalculateAsync(
            ctx.BranchUser.BranchId, ctx.Account.Id, TransactionDate, close.Id, productId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldWithholdCurrentVariance_WhenCloseIsDraft()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        SeedClose(ctx, DailyCloseStatus.Draft, currentVariance: 425m);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        // Draft counts are still moving — surface the open close + delta but withhold a number.
        response.Impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Draft);
        response.Impact.CashVarianceImpact.AccountId.ShouldBe(ctx.Account.Id);
        response.Impact.CashVarianceImpact.CurrentVariance.ShouldBeNull();
        response.Impact.CashVarianceImpact.ProjectedVariance.ShouldBeNull();
        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-100m);
        await ctx.CashVarianceCalculator.DidNotReceive().CalculateAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldFallBackToBranchClockToday_WhenAsOfDateIsOmitted()
    {
        var ctx = BuildContext(Role.Admin, AccountType.Tab, Direction.Out, value: 200m, withClient: true);
        var useCase = CreateUseCase(ctx);

        // No explicit as-of date → resolves the clock's branch-local today (2025-05-10) → due
        // 2025-05-01 is 9 days out → Days0To30.
        var response = await useCase.Execute(ctx.Request);

        response.Impact.ReceivableImpact.BucketAfter.ShouldBe(AgingBucket.Days0To30);
        ctx.BranchClock.Received(1).UtcNow();
        ctx.BranchClock.Received(1).LocalBusinessDate(ctx.BranchClockUtcNow);
    }

    [Fact]
    public async Task Execute_ShouldComputeImpact_WhenMemberActsOnLinkedAccount()
    {
        // Preview/write parity: a Member with a linked operator + an allowed account can preview the
        // same create they could commit.
        var ctx = BuildContext(Role.Member, AccountType.Terminal, Direction.In, value: 80m, withClient: false);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(-80m);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberTargetsUnlinkedAccount()
    {
        var ctx = BuildContext(Role.Member, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        // Member has a linked operator but no link to the targeted account.
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator.Id, [])
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenMemberHasNoLinkedOperator()
    {
        // The RecordedByOperator resolver runs before the account-scope guard, so the create flow
        // surfaces this as an OnValidationException, not a 403 — preview mirrors that exactly.
        var ctx = BuildContext(Role.Member, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Execute_ShouldComputeImpact_WhenAdminPreviewsCreate()
    {
        var ctx = BuildContext(Role.Admin, AccountType.Terminal, Direction.Out, value: 50m, withClient: false);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request, ctx.AsOfDate);

        response.Impact.CashVarianceImpact.VarianceDelta.ShouldBe(50m);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenAccountMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Account.Id, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionTypeMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        ctx.TransactionTypesRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithCategoryAsNoTrackingReturns(ctx.TransactionType.Id, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenClientMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Tab, Direction.Out, value: 200m, withClient: true);
        ctx.ClientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.ClientId!.Value, ctx.BranchUser.BranchId, null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoInvariantViolated()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.Out, value: 200m, withClient: true);
        // Mark the resolved type as fiado while the account is Terminal → §6.1 conflict.
        ctx.TransactionType.RequiresTabAccountAndClient = true;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksTarget()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting { BranchId = ctx.BranchUser.BranchId, LockDate = TransactionDate })
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidation_WhenValueIsNotPositive()
    {
        var ctx = BuildContext(Role.Manager, AccountType.Terminal, Direction.In, value: 100m, withClient: false);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(TransactionDate)
            .WithValue(0m)
            .WithTransactionTypeId(ctx.TransactionType.Id)
            .WithAccountId(ctx.Account.Id)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request, ctx.AsOfDate));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    private static PreviewCreateTransactionUseCase CreateUseCase(TestContext ctx)
    {
        var recordedByOperatorResolver = new TransactionRecordedByOperatorResolver();
        var branchConsistencyService = new TransactionBranchConsistencyService(
            ctx.AccountsRepository,
            ctx.OperatorsRepository,
            ctx.ClientsRepository,
            ctx.TransactionTypesRepository);
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var preamble = new TransactionCreatePreamble(
            ctx.AuthenticationService,
            memberAccountScopeResolver,
            recordedByOperatorResolver,
            branchConsistencyService,
            memberAccountScopeGuard,
            lockDateGuard,
            ctx.BranchHolidaySource);

        var projector = new TransactionEditImpactProjector(
            ctx.DailyClosesRepository,
            ctx.ClientsRepository,
            ctx.CashVarianceCalculator,
            ctx.CashVarianceProductResolver);

        return new PreviewCreateTransactionUseCase(
            ctx.AuthenticationService,
            preamble,
            projector,
            ctx.BranchClock);
    }

    /// <summary>
    /// Seeds a daily close for the create's (account, date) and configures the cash-variance product
    /// resolver + calculator so the real projector can live-recompute. Returns the product id and the
    /// close so callers can assert exact-argument forwarding.
    /// </summary>
    private static (Guid ProductId, DailyClose Close) SeedClose(TestContext ctx, DailyCloseStatus status, decimal currentVariance)
    {
        var close = new DailyClose
        {
            Id = Guid.NewGuid(),
            BranchId = ctx.BranchUser.BranchId,
            AccountId = ctx.Account.Id,
            Date = TransactionDate,
            Status = status
        };
        ctx.DailyClosesRepository = new DailyClosesRepositoryBuilder()
            .GetByBranchIdAndAccountIdAndDateAsNoTrackingReturns(ctx.BranchUser.BranchId, ctx.Account.Id, TransactionDate, close)
            .Build();

        var productId = Guid.NewGuid();
        ctx.CashVarianceProductResolver.GetIdAsync(ctx.BranchUser.BranchId, Arg.Any<CancellationToken>()).Returns(productId);
        ctx.CashVarianceCalculator
            .CalculateAsync(ctx.BranchUser.BranchId, ctx.Account.Id, TransactionDate, close.Id, productId, Arg.Any<CancellationToken>())
            .Returns(currentVariance);

        return (productId, close);
    }

    private static TestContext BuildContext(
        Role role,
        AccountType accountType,
        Direction direction,
        decimal value,
        bool withClient)
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
            .WithType(accountType)
            .Build();

        var branch = new BranchBuilder().WithId(branchUser.BranchId).Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Cat",
            DefaultDirection = direction,
            BranchId = branchUser.BranchId,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .WithSettlementRule(SettlementRule.SameDay)
            .WithRequiresTabAccountAndClient(false)
            .Build();

        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(account)
            .Build();

        Guid? clientId = withClient ? Guid.NewGuid() : null;
        var clientsRepositoryBuilder = new ClientsRepositoryBuilder();
        if (clientId is { } resolvedClientId)
        {
            clientsRepositoryBuilder.GetActiveByIdAndBranchIdAsNoTracking(
                resolvedClientId,
                branchUser.BranchId,
                new ClientBuilder().WithId(resolvedClientId).WithBranchId(branchUser.BranchId).WithName("Headline Client").Build());
        }

        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(TransactionDate)
            .WithValue(value)
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(accountId)
            .WithClientId(clientId)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId, callerOperator)
            .GetActiveByIdAndBranchIdAsNoTracking(callerOperator.Id, branchUser.BranchId, callerOperator)
            .Build();
        var accountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(accountId, branchUser.BranchId, account)
            .Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithCategoryAsNoTrackingReturns(transactionType.Id, branchUser.BranchId, transactionType)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var branchHolidaySource = Substitute.For<IBranchHolidaySource>();
        branchHolidaySource.GetHolidayDatesAsync(branchUser.BranchId).Returns(new HashSet<DateOnly>());

        var branchClockUtcNow = new DateTime(2025, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(branchClockUtcNow);
        branchClock.LocalBusinessDate(branchClockUtcNow).Returns(new DateTime(2025, 5, 10));

        return new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            Account = account,
            TransactionType = transactionType,
            ClientId = clientId,
            Request = request,
            AsOfDate = new DateTime(2025, 5, 15),
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            AccountsRepository = accountsRepository,
            ClientsRepository = clientsRepositoryBuilder.Build(),
            TransactionTypesRepository = transactionTypesRepository,
            SettingsRepository = settingsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            BranchHolidaySource = branchHolidaySource,
            DailyClosesRepository = new DailyClosesRepositoryBuilder().Build(),
            CashVarianceCalculator = Substitute.For<ICashVarianceCalculator>(),
            CashVarianceProductResolver = Substitute.For<ICashVarianceProductResolver>(),
            BranchClock = branchClock,
            BranchClockUtcNow = branchClockUtcNow
        };
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator CallerOperator { get; init; }
        public required Account Account { get; init; }
        public required TransactionType TransactionType { get; init; }
        public required Guid? ClientId { get; init; }
        public required RequestCreateTransactionJson Request { get; set; }
        public required DateTime AsOfDate { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required ITransactionTypesRepository TransactionTypesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required IBranchHolidaySource BranchHolidaySource { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; set; }
        public required ICashVarianceCalculator CashVarianceCalculator { get; init; }
        public required ICashVarianceProductResolver CashVarianceProductResolver { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required DateTime BranchClockUtcNow { get; init; }
    }
}
