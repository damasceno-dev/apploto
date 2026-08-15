using System.Globalization;
using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.Services.Holidays;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Transactions.CreateInstallment;

public class CreateTransactionInstallmentUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateManualInstallmentsWithExactSumSharedOriginDatesAndDescriptions()
    {
        var firstDueDate = DateTime.Today.AddDays(30);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription("Cliente informou datas dos cheques")
            .WithValue(333.33m)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = firstDueDate,
                    Value = 100.01m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = firstDueDate.AddDays(13),
                    Value = 111.11m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = firstDueDate.AddDays(40),
                    Value = 122.21m
                }
            ])
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        var response = await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(3);
        rows.Sum(row => row.Value).ShouldBe(ctx.Request.Value);
        response.Installments.Count.ShouldBe(3);
        response.Installments.Sum(installment => installment.Value).ShouldBe(ctx.Request.Value);

        var originId = rows[0].Id;
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Value.ShouldBe(ctx.Request.Installments[i].Value);
            rows[i].DueDate.ShouldBe(ctx.Request.Installments[i].DueDate);
            rows[i].OriginTransactionId.ShouldBe(originId);
            rows[i].Description.ShouldBe($"CH PRE ({i + 1}/3) - Cliente informou datas dos cheques");
        }

        rows[0].OriginTransactionId.ShouldBe(rows[0].Id);

        await ctx.TransactionsRepository.Received(1)
            .AddRange(Arg.Is<IEnumerable<Transaction>>(rows => rows.Count() == 3));
        await ctx.DailyCloseLedgerGuard.Received(1).EnsureLedgerAcceptsNewRow(
            ctx.BranchUser.BranchId,
            ctx.Request.AccountId,
            ctx.Account.Type,
            ctx.Request.Date.Date,
            Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.Received(1).Commit();
        await ctx.DailyCloseLedgerCoordinationScope.Received(1).Complete(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2, "100.01")]
    [InlineData(3, "333.33")]
    [InlineData(12, "1000.00")]
    public async Task Execute_ShouldAutoGenerateInstallmentsWithExactSumSharedOriginAndMonthlyDueDates(
        int installmentCount,
        string totalText)
    {
        var total = decimal.Parse(totalText, CultureInfo.InvariantCulture);
        var baseDueDate = DateTime.Today.AddDays(30);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription(null)
            .WithDueDate(baseDueDate)
            .WithValue(total)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithAutoGeneration(installmentCount, baseDueDate)
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        var response = await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(installmentCount);
        rows.Sum(row => row.Value).ShouldBe(total);
        response.Installments.Sum(installment => installment.Value).ShouldBe(total);

        var originId = rows[0].Id;
        for (var i = 0; i < installmentCount; i++)
        {
            rows[i].DueDate.ShouldBe(DueDateCalculator.AdjustToNextBusinessDay(baseDueDate.AddMonths(i)));
            rows[i].OriginTransactionId.ShouldBe(originId);
            rows[i].Description.ShouldBe($"CH PRE ({i + 1}/{installmentCount})");
        }

        rows[0].OriginTransactionId.ShouldBe(rows[0].Id);

        await ctx.TransactionsRepository.Received(1)
            .AddRange(Arg.Is<IEnumerable<Transaction>>(rows => rows.Count() == installmentCount));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldMoveAutoGeneratedWeekendDueDateToNextBusinessDay()
    {
        var saturday = NextDayOfWeek(DateTime.Today.AddDays(2), DayOfWeek.Saturday);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(100m)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithAutoGeneration(2, saturday)
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows[0].DueDate.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        rows[0].DueDate.ShouldBe(saturday.AddDays(2));
    }

    [Fact]
    public async Task Execute_ShouldMoveAutoGeneratedHolidayDueDateToNextBusinessDay()
    {
        // Auto-staggered monthly due date that lands on a branch holiday shifts to the
        // next non-weekend, non-holiday business day. Pick a Tuesday in the future as
        // the seed due date so the second installment lands one calendar month later
        // (still a weekday), and seed THAT date as a holiday.
        var seedDueDate = NextDayOfWeek(DateTime.Today.AddDays(10), DayOfWeek.Tuesday);
        var firstInstallmentDate = seedDueDate;
        var secondInstallmentSeed = seedDueDate.AddMonths(1);

        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.BranchHolidaySource.GetHolidayDatesAsync(ctx.BranchUser.BranchId)
            .Returns(new HashSet<DateOnly> { DateOnly.FromDateTime(secondInstallmentSeed) });

        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(100m)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithAutoGeneration(2, seedDueDate)
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows[0].DueDate.ShouldBe(firstInstallmentDate);
        rows[1].DueDate.ShouldBeGreaterThan(secondInstallmentSeed);
        rows[1].DueDate.DayOfWeek.ShouldNotBe(DayOfWeek.Saturday);
        rows[1].DueDate.DayOfWeek.ShouldNotBe(DayOfWeek.Sunday);
        DateOnly.FromDateTime(rows[1].DueDate)
            .ShouldNotBe(DateOnly.FromDateTime(secondInstallmentSeed));
    }

    [Theory]
    [InlineData(6, "0.10")]
    [InlineData(7, "0.11")]
    public async Task Execute_ShouldThrowOnValidation_WhenAutoGeneratedSplitCreatesNonPositiveRows(
        int installmentCount,
        string totalText)
    {
        var total = decimal.Parse(totalText, CultureInfo.InvariantCulture);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(total)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithAutoGeneration(installmentCount, DateTime.Today.AddDays(30))
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_ROW_VALUE_MUST_BE_POSITIVE);
        await ctx.TransactionsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Transaction>>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenTransactionTypeIsNotCheque()
    {
        var ctx = BuildHappyPathContext(SettlementRule.SameDay);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);
        await ctx.TransactionsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Transaction>>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_ShouldFallBackToPrefixOnly_WhenManualDescriptionIsNullOrWhitespace(string? description)
    {
        var firstDueDate = DateTime.Today.AddDays(30);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription(description)
            .WithValue(300m)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(30), Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(60), Value = 100m }
            ])
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(3);
        for (var i = 0; i < rows.Count; i++)
        {
            rows[i].Description.ShouldBe($"CH PRE ({i + 1}/3)");
        }
    }

    [Fact]
    public async Task Execute_ShouldKeepLongestManualDescriptionAtDatabaseLimit_WhenDescriptionUsesReservedPrefixBudget()
    {
        var installmentCount = CreateTransactionInstallmentFluentValidation.MaximumInstallments;
        var longestPrefix = $"CH PRE ({installmentCount}/{installmentCount}) - ";
        longestPrefix.Length.ShouldBe(TransactionValidationExtensions.InstallmentDescriptionPrefixReserve);

        var description = new string(
            'a',
            TransactionValidationExtensions.TransactionDescriptionMaxLength -
            TransactionValidationExtensions.InstallmentDescriptionPrefixReserve);
        var firstDueDate = DateTime.Today.AddDays(30);
        var perInstallmentValue = 10m;
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithDescription(description)
            .WithValue(perInstallmentValue * installmentCount)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithInstallments(Enumerable.Range(0, installmentCount)
                .Select(index => new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = firstDueDate.AddDays(index),
                    Value = perInstallmentValue
                })
                .ToList())
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(installmentCount);
        var lastDescription = rows[^1].Description;
        lastDescription.ShouldNotBeNull();
        lastDescription!.ShouldStartWith(longestPrefix);
        lastDescription.Length.ShouldBe(TransactionValidationExtensions.TransactionDescriptionMaxLength);
    }

    [Fact]
    public async Task Execute_ShouldPersistManualInstallmentsAsDraft_WhenSaveAsDraftIsTrue()
    {
        var firstDueDate = DateTime.Today.AddDays(30);
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(300m)
            .WithSaveAsDraft(true)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate, Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(30), Value = 100m },
                new RequestCreateTransactionInstallmentItemJson { DueDate = firstDueDate.AddDays(60), Value = 100m }
            ])
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(3);
        rows.ShouldAllBe(row => row.Status == TransactionStatus.Draft);
    }

    [Fact]
    public async Task Execute_ShouldPersistAutoGeneratedInstallmentsAsDraft_WhenSaveAsDraftIsTrue()
    {
        var ctx = BuildHappyPathContext(SettlementRule.OperatorEnteredCheque);
        ctx.Request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithValue(600m)
            .WithSaveAsDraft(true)
            .WithTransactionTypeId(ctx.Request.TransactionTypeId)
            .WithAccountId(ctx.Request.AccountId)
            .WithAutoGeneration(4, DateTime.Today.AddDays(30))
            .Build();

        var useCase = CreateUseCaseCapturingRows(ctx, out var capturedRowsAccessor);

        await useCase.Execute(ctx.Request);

        var rows = capturedRowsAccessor();
        rows.Count.ShouldBe(4);
        rows.ShouldAllBe(row => row.Status == TransactionStatus.Draft);
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_BeforeNonChequeConflict_WhenMemberTargetsUnlinkedAccount()
    {
        // MemberAccountScopeGuard must run inside the shared preamble — before the use case
        // checks SettlementRule. A Member with an out-of-scope account on a non-cheque type
        // must surface 403 (scope), not 409 (non-cheque).
        var ctx = BuildHappyPathContext(SettlementRule.SameDay, Role.Member);
        ctx.OperatorAccountsRepository = new OperatorAccountsRepositoryBuilder()
            .ListActiveByOperatorIdAsNoTracking(ctx.CallerOperator.Id, [])
            .Build();

        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ctx.TransactionsRepository.DidNotReceive().AddRange(Arg.Any<IEnumerable<Transaction>>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private static CreateTransactionInstallmentUseCase CreateUseCaseCapturingRows(
        HappyPathContext ctx,
        out Func<IReadOnlyList<Transaction>> capturedRowsAccessor)
    {
        IReadOnlyList<Transaction>? capturedRows = null;
        ctx.TransactionsRepository
            .AddRange(Arg.Do<IEnumerable<Transaction>>(rows => capturedRows = rows.ToList()))
            .Returns(Task.CompletedTask);
        capturedRowsAccessor = () =>
        {
            capturedRows.ShouldNotBeNull();
            return capturedRows!;
        };

        return CreateUseCase(ctx);
    }

    private static CreateTransactionInstallmentUseCase CreateUseCase(HappyPathContext ctx)
    {
        var recordedByOperatorResolver = new TransactionRecordedByOperatorResolver();
        var branchConsistencyService = new TransactionBranchConsistencyService(
            ctx.AccountsRepository,
            ctx.OperatorsRepository,
            ctx.ClientsRepository,
            ctx.TransactionTypesRepository);
        var lockDateGuard = new LockDateGuard(new LockDateReader(ctx.SettingsRepository));
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var memberAccountScopeGuard = new MemberAccountScopeGuard();
        var transactionCreatePreamble = new TransactionCreatePreamble(
            ctx.AuthenticationService,
            memberAccountScopeResolver,
            recordedByOperatorResolver,
            branchConsistencyService,
            memberAccountScopeGuard,
            lockDateGuard,
            ctx.BranchHolidaySource);
        var installmentPlanBuilder = new InstallmentPlanBuilder();

        return new CreateTransactionInstallmentUseCase(
            transactionCreatePreamble,
            installmentPlanBuilder,
            ctx.TransactionsRepository,
            ctx.DailyCloseLedgerGuard,
            ctx.DailyCloseLedgerCoordination,
            ctx.UnitOfWork);
    }

    private static HappyPathContext BuildHappyPathContext(SettlementRule settlementRule, Role role = Role.Manager)
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
            DefaultDirection = Direction.In,
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

        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var dailyCloseLedgerGuard = Substitute.For<IDailyCloseLedgerGuard>();
        var coordinationBuilder = new DailyCloseAccountCoordinationBuilder();
        var unitOfWork = new UnitOfWorkBuilder().Build();
        var branchHolidaySource = Substitute.For<IBranchHolidaySource>();
        branchHolidaySource.GetHolidayDatesAsync(branchUser.BranchId).Returns(new HashSet<DateOnly>());

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
            DailyCloseLedgerGuard = dailyCloseLedgerGuard,
            DailyCloseLedgerCoordination = coordinationBuilder.Build(),
            DailyCloseLedgerCoordinationScope = coordinationBuilder.Scope,
            UnitOfWork = unitOfWork,
            BranchHolidaySource = branchHolidaySource
        };
    }

    private static DateTime NextDayOfWeek(DateTime start, DayOfWeek dayOfWeek)
    {
        var result = start.Date;

        while (result.DayOfWeek != dayOfWeek)
        {
            result = result.AddDays(1);
        }

        return result;
    }

    private class HappyPathContext
    {
        public required BranchUser BranchUser { get; set; }
        public required Operator CallerOperator { get; set; }
        public required Account Account { get; set; }
        public required TransactionType TransactionType { get; set; }
        public required RequestCreateTransactionInstallmentJson Request { get; set; }
        public required IAuthenticationService AuthenticationService { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IAccountsRepository AccountsRepository { get; set; }
        public required IClientsRepository ClientsRepository { get; set; }
        public required ITransactionTypesRepository TransactionTypesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required ITransactionsRepository TransactionsRepository { get; set; }
        public required IDailyCloseLedgerGuard DailyCloseLedgerGuard { get; set; }
        public required IDailyCloseAccountCoordination DailyCloseLedgerCoordination { get; set; }
        public required IDailyCloseAccountCoordinationScope DailyCloseLedgerCoordinationScope { get; set; }
        public required IUnitOfWork UnitOfWork { get; set; }
        public required IBranchHolidaySource BranchHolidaySource { get; set; }
    }
}
