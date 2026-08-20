using CommonTestUtilities.Entities;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Settings.LockMonth;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Settings.LockMonth;

public sealed class LockSettingMonthUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2025, 7, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FixedToday = new(2025, 7, 15);

    [Fact]
    public async Task Execute_ShouldLandOnMonthEnd_WhenWholeIntervalIsReady()
    {
        var ctx = BuildContext(branchCreatedAt: new DateTime(2025, 5, 3, 12, 0, 0, DateTimeKind.Utc));

        var response = await CreateUseCase(ctx).Execute(Request(2025, 6));

        response.LockDate.ShouldBe(new DateTime(2025, 6, 30));
        ctx.Setting.LockDate.ShouldBe(new DateTime(2025, 6, 30));
        await ctx.UnitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
        await ctx.CoordinationScope.Received(1).Complete(Arg.Any<CancellationToken>());
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 6, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(DailyCloseStatus.Draft)]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Rejected)]
    public async Task Execute_ShouldReturnExactUnapprovedCloseKey(DailyCloseStatus status)
    {
        var ctx = BuildContext();
        var close = ApprovedClose(ctx, new DateTime(2025, 5, 4));
        close.Status = status;
        SetMonth(ctx, 2025, 5, closes: [close]);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_UNAPPROVED_CLOSE);
    }

    [Fact]
    public async Task Execute_ShouldReturnExactDraftTransactionKey()
    {
        var ctx = BuildContext();
        SetMonth(ctx, 2025, 5, counts:
        [
            new MonthlyTransactionCountRow(new DateTime(2025, 5, 2), TransactionStatus.Draft, 1)
        ]);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_DRAFT_TRANSACTIONS);
    }

    [Fact]
    public async Task Execute_ShouldReturnExactNoCloseMonthKey_WhenExpectedActivityExistsWithoutAnyClose()
    {
        var ctx = BuildContext();
        SetMonth(ctx, 2025, 5, activity:
        [
            new TerminalActivityPairRow(new DateTime(2025, 5, 6), Guid.NewGuid(), "Terminal A")
        ]);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_NO_CLOSE_WITH_EXPECTED_ACTIVITY);
    }

    [Fact]
    public async Task Execute_ShouldReturnExactMissingExpectedCloseKey_WhenAnotherCloseExists()
    {
        var ctx = BuildContext();
        SetMonth(
            ctx,
            2025,
            5,
            closes: [ApprovedClose(ctx, new DateTime(2025, 5, 5))],
            activity:
            [
                new TerminalActivityPairRow(new DateTime(2025, 5, 6), Guid.NewGuid(), "Terminal B")
            ]);

        await AssertConflict(ctx, ResourcesErrorMessages.SETTING_LOCK_MONTH_MISSING_EXPECTED_CLOSE);
    }

    [Fact]
    public async Task Execute_ShouldRejectJuneWithIntermediateKey_WhenMayIsUnresolved()
    {
        var ctx = BuildContext(branchCreatedAt: new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc));
        SetMonth(ctx, 2025, 5, closes: [
            new DailyCloseBuilder()
                .WithBranchId(ctx.Branch.Id)
                .WithAccount(new AccountBuilder().WithBranchId(ctx.Branch.Id).Build())
                .WithDate(new DateTime(2025, 5, 5))
                .WithStatus(DailyCloseStatus.Draft)
                .Build()
        ]);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 6)));

        exception.Message.ShouldBe(IntermediateUnresolvedMessage(2025, 5));
        ctx.Setting.LockDate.ShouldBe(DateTime.MinValue);
    }

    [Fact]
    public async Task Execute_ShouldUseBranchCreatedAtMonthAsFreshBranchFloor()
    {
        var ctx = BuildContext(branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc));

        await CreateUseCase(ctx).Execute(Request(2025, 6));

        await ctx.DailyClosesRepository.DidNotReceive().ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 6, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldValidateEarlierCloseMonth_WhenOperationalDataPredatesCreatedAtFloor()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc),
            earliestCloseDate: new DateTime(2025, 5, 20));
        SetMonth(ctx, 2025, 5, closes:
        [
            new DailyCloseBuilder()
                .WithBranchId(ctx.Branch.Id)
                .WithAccount(new AccountBuilder().WithBranchId(ctx.Branch.Id).Build())
                .WithDate(new DateTime(2025, 5, 20))
                .WithStatus(DailyCloseStatus.Draft)
                .Build()
        ]);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 6)));

        exception.Message.ShouldBe(IntermediateUnresolvedMessage(2025, 5));
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldValidateEarlierTransactionMonth_WhenOperationalDataPredatesCreatedAtFloor()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc),
            earliestTransactionDate: new DateTime(2025, 5, 21));
        SetMonth(ctx, 2025, 5, counts:
        [
            new MonthlyTransactionCountRow(new DateTime(2025, 5, 21), TransactionStatus.Draft, 1)
        ]);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 6)));

        exception.Message.ShouldBe(IntermediateUnresolvedMessage(2025, 5));
        await ctx.TransactionsRepository.Received(1)
            .CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.UnitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldAllowCleanOperationalMonthBeforeCreatedAt_WithoutManagerRepair()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc),
            earliestTransactionDate: new DateTime(2025, 5, 21));
        SetMonth(ctx, 2025, 5, counts:
        [
            new MonthlyTransactionCountRow(new DateTime(2025, 5, 21), TransactionStatus.Active, 1)
        ]);

        var response = await CreateUseCase(ctx).Execute(Request(2025, 6));

        response.LockDate.ShouldBe(new DateTime(2025, 6, 30));
        await ctx.UnitOfWork.Received(1).Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldAllowLockingDataMonthBeforeCreatedAt_WhenThatMonthIsReady()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc),
            earliestTransactionDate: new DateTime(2025, 5, 21));

        var response = await CreateUseCase(ctx).Execute(Request(2025, 5));

        response.LockDate.ShouldBe(new DateTime(2025, 5, 31));
    }

    [Fact]
    public async Task Execute_ShouldReturnExactBranchFloorKey_WhenRequestedMonthPredatesCreatedAtMonth()
    {
        var ctx = BuildContext(branchCreatedAt: new DateTime(2025, 6, 18, 22, 0, 0, DateTimeKind.Utc));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 5)));

        exception.Message.ShouldBe(ResourcesErrorMessages.SETTING_LOCK_MONTH_BEFORE_BRANCH_FLOOR);
        await ctx.UnitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldReturnExactAlreadyLockedKey_OnSameMonthReplay()
    {
        var ctx = BuildContext(lockDate: new DateTime(2025, 5, 31));

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 5)));

        exception.Message.ShouldBe(ResourcesErrorMessages.SETTING_LOCK_MONTH_ALREADY_LOCKED);
        await ctx.DailyClosesRepository.DidNotReceive().ListByBranchIdAndYearMonthAsNoTracking(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldClampLegacyPartialBoundaryToOperationalFloor()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 5, 3, 12, 0, 0, DateTimeKind.Utc),
            lockDate: new DateTime(1, 1, 5));

        var response = await CreateUseCase(ctx).Execute(Request(2025, 5));

        response.LockDate.ShouldBe(new DateTime(2025, 5, 31));
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.DailyClosesRepository.DidNotReceive().ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 1, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRepairLegacyFutureBoundaryAfterValidatingElapsedInterval()
    {
        var ctx = BuildContext(
            branchCreatedAt: new DateTime(2025, 5, 3, 12, 0, 0, DateTimeKind.Utc),
            lockDate: new DateTime(2030, 12, 31));

        var response = await CreateUseCase(ctx).Execute(Request(2025, 6));

        response.LockDate.ShouldBe(new DateTime(2025, 6, 30));
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 5, Arg.Any<CancellationToken>());
        await ctx.DailyClosesRepository.Received(1).ListByBranchIdAndYearMonthAsNoTracking(
            ctx.Branch.Id, 2025, 6, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(2025, 7)]
    [InlineData(2025, 8)]
    public async Task Execute_ShouldReturnExactCurrentOrFutureKey(int year, int month)
    {
        var ctx = BuildContext();

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(year, month)));

        exception.Message.ShouldBe(ResourcesErrorMessages.SETTING_LOCK_MONTH_CURRENT_OR_FUTURE);
        await ctx.MonthLockCoordination.DidNotReceive()
            .TryAcquireExclusive(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldLockEmptyMonth_WhenThereIsNoExpectedActivity()
    {
        var ctx = BuildContext();

        var response = await CreateUseCase(ctx).Execute(Request(2025, 5));

        response.LockDate.ShouldBe(new DateTime(2025, 5, 31));
    }

    [Fact]
    public async Task Execute_ShouldReturnExactCoordinationBusyKey_WhenExclusiveBoundaryTimesOut()
    {
        var ctx = BuildContext(coordinationUnavailable: true);

        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 5)));

        exception.Message.ShouldBe(ResourcesErrorMessages.SETTING_LOCK_MONTH_COORDINATION_BUSY);
        await ctx.UnitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_ShouldRejectMemberAsDefenseInDepth()
    {
        var ctx = BuildContext(role: Role.Member);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 5)));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await ctx.MonthLockCoordination.DidNotReceive()
            .TryAcquireExclusive(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private static async Task AssertConflict(TestContext ctx, string expectedMessage)
    {
        var exception = await Should.ThrowAsync<ConflictException>(() =>
            CreateUseCase(ctx).Execute(Request(2025, 5)));

        exception.Message.ShouldBe(expectedMessage);
        await ctx.UnitOfWork.DidNotReceive().Commit(Arg.Any<CancellationToken>());
    }

    private static DailyClose ApprovedClose(TestContext ctx, DateTime date)
    {
        var account = new AccountBuilder().WithBranchId(ctx.Branch.Id).Build();
        return new DailyCloseBuilder()
            .WithBranchId(ctx.Branch.Id)
            .WithAccount(account)
            .WithDate(date)
            .WithStatus(DailyCloseStatus.Approved)
            .Build();
    }

    private static RequestLockSettingMonthJson Request(int year, int month)
    {
        return new RequestLockSettingMonthJson { Year = year, Month = month };
    }

    private static string IntermediateUnresolvedMessage(int year, int month)
    {
        return string.Format(
            ResourcesErrorMessages.SETTING_LOCK_MONTH_INTERMEDIATE_UNRESOLVED,
            $"{month:00}/{year}");
    }

    private static void SetMonth(
        TestContext ctx,
        int year,
        int month,
        IReadOnlyList<DailyClose>? closes = null,
        IReadOnlyList<MonthlyTransactionCountRow>? counts = null,
        IReadOnlyList<TerminalActivityPairRow>? activity = null)
    {
        ctx.DailyClosesRepository.ListByBranchIdAndYearMonthAsNoTracking(
                ctx.Branch.Id, year, month, Arg.Any<CancellationToken>())
            .Returns(closes ?? []);
        ctx.TransactionsRepository.CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                ctx.Branch.Id, year, month, Arg.Any<CancellationToken>())
            .Returns(counts ?? []);
        ctx.TransactionsRepository.ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                ctx.Branch.Id, year, month, Arg.Any<CancellationToken>())
            .Returns(activity ?? []);
    }

    private static TestContext BuildContext(
        DateTime? branchCreatedAt = null,
        DateTime? lockDate = null,
        Role role = Role.Manager,
        DateTime? earliestCloseDate = null,
        DateTime? earliestTransactionDate = null,
        bool coordinationUnavailable = false)
    {
        var branch = new BranchBuilder()
            .WithCreatedAt(branchCreatedAt ?? new DateTime(2025, 5, 1, 12, 0, 0, DateTimeKind.Utc))
            .Build();
        var branchUser = new BranchUserBuilder()
            .WithBranch(branch)
            .WithRole(role)
            .Build();
        var setting = new SettingBuilder()
            .WithBranchId(branch.Id)
            .WithLockDate(lockDate ?? DateTime.MinValue)
            .Build();
        var dailyClosesRepository = Substitute.For<IDailyClosesRepository>();
        dailyClosesRepository.GetEarliestDateByBranchIdAsNoTracking(
                branch.Id,
                Arg.Any<CancellationToken>())
            .Returns(earliestCloseDate);
        dailyClosesRepository.ListByBranchIdAndYearMonthAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var transactionsRepository = Substitute.For<ITransactionsRepository>();
        transactionsRepository.GetEarliestDateByBranchIdAsNoTracking(
                branch.Id,
                Arg.Any<CancellationToken>())
            .Returns(earliestTransactionDate);
        transactionsRepository.CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        transactionsRepository.ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var coordinationBuilder = new MonthLockCoordinationBuilder();
        if (coordinationUnavailable)
            coordinationBuilder.ExclusiveUnavailable();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDate(FixedUtcNow).Returns(FixedToday);
        branchClock.LocalBusinessDate(branch.CreatedAt).Returns(branch.CreatedAt.Date);

        return new TestContext
        {
            Branch = branch,
            Setting = setting,
            AuthenticationService = new AuthenticationServiceBuilder()
                .GetAuthenticatedBranchUser(branchUser)
                .Build(),
            SettingsRepository = BuildSettingsRepository(branch.Id, setting),
            BranchesRepository = BuildBranchesRepository(branch),
            DailyClosesRepository = dailyClosesRepository,
            TransactionsRepository = transactionsRepository,
            BranchClock = branchClock,
            MonthLockCoordination = coordinationBuilder.Build(),
            CoordinationScope = coordinationBuilder.ExclusiveScope,
            UnitOfWork = new UnitOfWorkBuilder().Build()
        };
    }

    private static ISettingsRepository BuildSettingsRepository(Guid branchId, Setting setting)
    {
        var repository = Substitute.For<ISettingsRepository>();
        repository.GetByBranchId(branchId, Arg.Any<CancellationToken>()).Returns(setting);
        return repository;
    }

    private static IBranchesRepository BuildBranchesRepository(Branch branch)
    {
        var repository = Substitute.For<IBranchesRepository>();
        repository.GetById(branch.Id, Arg.Any<CancellationToken>()).Returns(branch);
        return repository;
    }

    private static LockSettingMonthUseCase CreateUseCase(TestContext ctx)
    {
        return new LockSettingMonthUseCase(
            ctx.AuthenticationService,
            ctx.SettingsRepository,
            ctx.BranchesRepository,
            ctx.DailyClosesRepository,
            ctx.TransactionsRepository,
            new MonthLockReadinessEvaluator(),
            ctx.BranchClock,
            ctx.MonthLockCoordination,
            ctx.UnitOfWork);
    }

    private sealed class TestContext
    {
        public required Branch Branch { get; init; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required IBranchesRepository BranchesRepository { get; init; }
        public required IDailyClosesRepository DailyClosesRepository { get; init; }
        public required ITransactionsRepository TransactionsRepository { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IMonthLockCoordination MonthLockCoordination { get; init; }
        public required IMonthLockCoordinationScope CoordinationScope { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }
}
