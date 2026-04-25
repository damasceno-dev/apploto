using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Create;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.Create;

public class CreateTransactionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateActiveTransaction_WhenValidRequestForManager()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(TransactionStatus.Active);
        response.CategoryId.ShouldBe(ctx.TransactionType.CategoryId);
        response.Direction.ShouldBe(Direction.In);
        response.BranchId.ShouldBe(ctx.BranchUser.BranchId);
        response.RecordedByOperatorId.ShouldBe(ctx.CallerOperator.Id);
        response.DueDate.ShouldBe(ctx.Request.Date);

        await ctx.TransactionsRepository.Received(1).Add(Arg.Is<Transaction>(t =>
            t.BranchId == ctx.BranchUser.BranchId &&
            t.AccountId == ctx.Request.AccountId &&
            t.TransactionTypeId == ctx.TransactionType.Id &&
            t.CategoryId == ctx.TransactionType.CategoryId &&
            t.Direction == Direction.In &&
            t.Status == TransactionStatus.Active &&
            t.CreatedByUserId == ctx.BranchUser.UserId &&
            t.RecordedByOperatorId == ctx.CallerOperator.Id));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDenormalizeFromResolvedType_NotFromRequest()
    {
        // Direction on the resolved TransactionType's Category is Out; Request carries no direction.
        var ctx = BuildHappyPathContext(Role.Admin, SettlementRule.SameDay, Direction.Out);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Direction.ShouldBe(Direction.Out);
        response.CategoryId.ShouldBe(ctx.TransactionType.CategoryId);
        await ctx.TransactionsRepository.Received(1).Add(Arg.Is<Transaction>(t =>
            t.CategoryId == ctx.TransactionType.CategoryId &&
            t.Direction == Direction.Out));
    }

    [Fact]
    public async Task Execute_ShouldSaveAsDraft_WhenSaveAsDraftIsTrue()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithValue(ctx.Request.Value)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithSaveAsDraft(true)
            .Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Theory]
    [InlineData(SettlementRule.SameDay, 0)]
    [InlineData(SettlementRule.NextCalendarDay, 1)]
    public async Task Execute_ShouldComputeDueDate_ForCalendarRules(SettlementRule rule, int expectedDaysAhead)
    {
        var date = new DateTime(2025, 3, 10); // Monday
        var ctx = BuildHappyPathContext(Role.Manager, rule, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.DueDate.ShouldBe(date.AddDays(expectedDaysAhead));
    }

    [Fact]
    public async Task Execute_ShouldComputeDueDate_NextBusinessDay_SkipWeekend()
    {
        var friday = new DateTime(2025, 3, 7); // Friday
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.NextBusinessDay, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(friday)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.DueDate.ShouldBe(new DateTime(2025, 3, 10)); // Monday
    }

    [Fact]
    public async Task Execute_ShouldComputeDueDate_TwoBusinessDays_SkipWeekend()
    {
        var thursday = new DateTime(2025, 3, 6);
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.TwoBusinessDays, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(thursday)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.DueDate.ShouldBe(new DateTime(2025, 3, 10)); // Friday -> Monday is skipped nothing, so Thu + 2 biz = Mon
    }

    [Fact]
    public async Task Execute_ShouldUseOperatorProvidedDueDate_WhenChequeRule()
    {
        var date = new DateTime(2025, 3, 1);
        var dueDate = new DateTime(2025, 4, 5);
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.OperatorEnteredCheque, Direction.Out);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(date)
            .WithDueDate(dueDate)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .Build();

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.DueDate.ShouldBe(dueDate);
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenChequeRuleWithoutDueDate()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.OperatorEnteredCheque, Direction.Out);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithDueDate(null)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenMemberSuppliesDifferentRecordedByOperatorId()
    {
        var ctx = BuildHappyPathContext(Role.Member, SettlementRule.SameDay, Direction.In);
        // Supplied value differs from the caller's linked operator — still rejected.
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithRecordedByOperatorId(Guid.NewGuid())
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenMemberSuppliesMatchingRecordedByOperatorId()
    {
        // Proves the rejection is unconditional on value: even when the supplied override
        // matches the caller's linked operator exactly, the server still rejects it —
        // Members are not allowed to set the field, period.
        var ctx = BuildHappyPathContext(Role.Member, SettlementRule.SameDay, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithRecordedByOperatorId(ctx.CallerOperator.Id)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_CANNOT_OVERRIDE_RECORDED_BY_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildHappyPathContext(Role.Member, SettlementRule.SameDay, Direction.In);
        // No linked operator for this user in this branch.
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenManagerHasNoOperatorLinkAndSuppliesNone()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        // Manager has no linked operator and the request carries no override.
        ctx.OperatorsRepository = new OperatorsRepositoryBuilder()
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId, null)
            .Build();
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithRecordedByOperatorId(null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_REQUIRES_RECORDED_BY_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldDefaultRecordedByOperatorId_ToCallerOperator_ForMember()
    {
        var ctx = BuildHappyPathContext(Role.Member, SettlementRule.SameDay, Direction.In);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.RecordedByOperatorId.ShouldBe(ctx.CallerOperator.Id);
    }

    [Fact]
    public async Task Execute_ShouldAllowManagerToOverrideRecordedByOperatorId()
    {
        var overrideOperatorId = Guid.NewGuid();
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithRecordedByOperatorId(overrideOperatorId)
            .Build();

        // Override operator must be resolvable in this branch.
        var overrideOperator = new OperatorBuilder()
            .WithId(overrideOperatorId)
            .WithBranchId(ctx.BranchUser.BranchId)
            .Build();
        ctx.OperatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(overrideOperatorId, ctx.BranchUser.BranchId)
            .Returns(overrideOperator);

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.RecordedByOperatorId.ShouldBe(overrideOperatorId);
        await ctx.OperatorsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(overrideOperatorId, ctx.BranchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberTargetsUnlinkedAccount()
    {
        var ctx = BuildHappyPathContext(Role.Member, SettlementRule.SameDay, Direction.In);
        // Swap the OperatorAccounts mock to return no links for this operator.
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator.Id, [])
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenAccountMissingOrCrossBranch()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.AccountId, ctx.BranchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTransactionTypeMissingOrCrossBranch()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        ctx.TransactionTypesRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithCategoryAsNoTrackingReturns(
                ctx.Request.TransactionTypeId,
                ctx.BranchUser.BranchId,
                null)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenClientMissingOrCrossBranch()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        var clientId = Guid.NewGuid();
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithClientId(clientId)
            .Build();
        ctx.ClientsRepository.GetActiveByIdAndBranchIdAsNoTracking(clientId, ctx.BranchUser.BranchId).Returns((Client?)null);

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoInvariantViolated_NonTabAccount()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.Out);
        // Mark the resolved type as fiado.
        ctx.TransactionType.RequiresTabAccountAndClient = true;
        var clientId = Guid.NewGuid();
        ctx.Request = new RequestCreateTransactionJsonBuilder()
            .WithDate(ctx.Request.Date)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithClientId(clientId)
            .Build();
        ctx.ClientsRepository.GetActiveByIdAndBranchIdAsNoTracking(clientId, ctx.BranchUser.BranchId)
            .Returns(new ClientBuilder().WithId(clientId).WithBranchId(ctx.BranchUser.BranchId).Build());
        // Account type is Terminal (non-tab by default)

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenFiadoInvariantViolated_NoClient()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.Out);
        ctx.TransactionType.RequiresTabAccountAndClient = true;
        // Account IS Tab, but ClientId null.
        var tabAccount = new AccountBuilder()
            .WithId(ctx.Request.AccountId)
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithType(AccountType.Tab)
            .Build();
        ctx.AccountsRepository = new AccountsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.AccountId, ctx.BranchUser.BranchId, tabAccount)
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksTarget()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        var lockedDate = ctx.Request.Date;
        ctx.SettingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(
                ctx.BranchUser.BranchId,
                new Setting
                {
                    BranchId = ctx.BranchUser.BranchId,
                    LockDate = lockedDate
                })
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldPassExpectedBranchIdArgsToScopedRepositoryQueries()
    {
        var ctx = BuildHappyPathContext(Role.Manager, SettlementRule.SameDay, Direction.In);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Request);

        await ctx.OperatorsRepository.Received(1)
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(ctx.BranchUser.UserId, ctx.BranchUser.BranchId);
        await ctx.AccountsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.AccountId, ctx.BranchUser.BranchId);
        await ctx.TransactionTypesRepository.Received(1)
            .GetActiveByIdAndBranchIdWithCategoryAsNoTracking(ctx.Request.TransactionTypeId, ctx.BranchUser.BranchId);
    }

    private static CreateTransactionUseCase CreateUseCase(HappyPathContext ctx)
    {
        var recordedByOperatorResolver = new TransactionRecordedByOperatorResolver();
        var branchConsistencyService = new TransactionBranchConsistencyService(
            ctx.AccountsRepository,
            ctx.OperatorsRepository,
            ctx.ClientsRepository,
            ctx.TransactionTypesRepository);
        var lockDateGuard = new LockDateGuard(ctx.SettingsRepository);
        var memberTransactionScopeResolver = new MemberTransactionScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var transactionCreatePreamble = new TransactionCreatePreamble(
            ctx.AuthenticationService,
            memberTransactionScopeResolver,
            recordedByOperatorResolver,
            branchConsistencyService,
            memberAccountScopeGuard,
            lockDateGuard);

        return new CreateTransactionUseCase(
            transactionCreatePreamble,
            ctx.TransactionsRepository,
            ctx.UnitOfWork);
    }

    private static HappyPathContext BuildHappyPathContext(
        Role role,
        SettlementRule settlementRule,
        Direction direction)
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

        var branch = new BranchBuilder().WithId(branchUser.BranchId).Build();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Entradas",
            DefaultDirection = direction,
            BranchId = branchUser.BranchId,
            Branch = branch
        };
        var transactionType = new TransactionTypeBuilder()
            .WithCategory(category)
            .WithSettlementRule(settlementRule)
            .WithRequiresTabAccountAndClient(false)
            .Build();

        var operatorAccount = new OperatorAccountBuilder()
            .WithOperator(callerOperator)
            .WithAccount(account)
            .Build();

        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(transactionType.Id)
            .WithAccountId(accountId)
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
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var transactionTypesRepository = new TransactionTypesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithCategoryAsNoTrackingReturns(transactionType.Id, branchUser.BranchId, transactionType)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, null)
            .Build();
        var operatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(callerOperator.Id, [operatorAccount])
            .Build();
        var transactionsRepository = new TransactionsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new HappyPathContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            Account = account,
            TransactionType = transactionType,
            Request = request,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            AccountsRepository = accountsRepository,
            ClientsRepository = clientsRepository,
            TransactionTypesRepository = transactionTypesRepository,
            SettingsRepository = settingsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            TransactionsRepository = transactionsRepository,
            UnitOfWork = unitOfWork
        };
    }

    private class HappyPathContext
    {
        public required BranchUser BranchUser { get; set; }
        public required Operator CallerOperator { get; set; }
        public required Account Account { get; set; }
        public required TransactionType TransactionType { get; set; }
        public required RequestCreateTransactionJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required ITransactionTypesRepository TransactionTypesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IUnitOfWork UnitOfWork { get; set; }
    }
}
