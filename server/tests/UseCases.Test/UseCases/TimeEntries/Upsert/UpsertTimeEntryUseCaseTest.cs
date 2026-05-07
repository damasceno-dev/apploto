using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.Upsert;

public class UpsertTimeEntryUseCaseTest
{
    private static readonly DateTime FixedNow = new(2026, 5, 5, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TodayDate = new(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc);

    // ── Insert happy path ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldInsert_WhenNoExistingEntryAndCallerIsManager()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.OperatorId.ShouldBe(ctx.TargetOperator.Id);
        response.OperatorName.ShouldBe(ctx.TargetOperator.Name);
        response.BranchId.ShouldBe(ctx.BranchUser.BranchId);
        response.Status.ShouldBe(TimeEntryStatus.Present);
        response.TotalHours.ShouldBe(8m);
        response.BalanceHours.ShouldBe(0.67m);
        response.UpdatedAt.ShouldBe(FixedNow);
        response.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.CreatedAt.ShouldBe(FixedNow);
        response.Active.ShouldBeTrue();

        await ctx.TimeEntriesRepository.Received(1).Add(Arg.Is<TimeEntry>(t =>
            t.OperatorId == ctx.TargetOperator.Id &&
            t.BranchId == ctx.BranchUser.BranchId &&
            t.Date == ctx.Request.Date &&
            t.Status == TimeEntryStatus.Present &&
            t.ClockIn == ctx.Request.ClockIn &&
            t.ClockOut == ctx.Request.ClockOut &&
            t.TotalHours == 8m &&
            t.BalanceHours == 0.67m &&
            t.UpdatedAt == FixedNow &&
            t.UpdatedByUserId == ctx.BranchUser.UserId));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    // ── In-place update happy path ────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldMutateExistingInPlace_AndPreserveId_WhenEntryExists()
    {
        var ctx = BuildContext(Role.Admin, linkedOperator: false);
        var existing = new TimeEntryBuilder()
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithOperatorId(ctx.TargetOperator.Id)
            .WithDate(ctx.Request.Date)
            .WithStatus(TimeEntryStatus.DayOff)
            .WithClockIn(null)
            .WithClockOut(null)
            .WithTotalHours(0m)
            .WithBalanceHours(-7.33m)
            .Build();
        ctx.TimeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(ctx.BranchUser.BranchId, ctx.TargetOperator.Id, ctx.Request.Date)
            .Returns(existing);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(existing.Id);
        existing.Status.ShouldBe(TimeEntryStatus.Present);
        existing.ClockIn.ShouldBe(ctx.Request.ClockIn);
        existing.ClockOut.ShouldBe(ctx.Request.ClockOut);
        existing.TotalHours.ShouldBe(8m);
        existing.BalanceHours.ShouldBe(0.67m);
        existing.UpdatedAt.ShouldBe(FixedNow);
        existing.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);

        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    // ── Calculation service receives Setting values ──────────────────────

    [Fact]
    public async Task Execute_ShouldUseSettingValuesForCalculation()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Setting.DailyTargetHours = 6m;
        ctx.Setting.LunchDeductionOver6H = 0.5m;
        ctx.Setting.LunchDeductionOver4H = 0.10m;
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        // 8:00 → 17:00 = 9h gross > 6h → deduct 0.5 → 8.5h total → balance 8.5 - 6 = 2.5
        response.TotalHours.ShouldBe(8.5m, tolerance: 0.001m);
        response.BalanceHours.ShouldBe(2.5m, tolerance: 0.001m);
    }

    // ── Permission outcomes — Member ──────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldSucceed_WhenMemberWritesOwnSameDayPresentEntry()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true);
        ctx.Request.OperatorId = ctx.CallerOperator!.Id;
        ctx.TargetOperator = ctx.CallerOperator;
        ctx.OperatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.CallerOperator.Id, ctx.BranchUser.BranchId)
            .Returns(ctx.CallerOperator);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.OperatorId.ShouldBe(ctx.CallerOperator.Id);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberHasNoLinkedOperator()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: false);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberTargetsAnotherOperator()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true);
        // Default: Request.OperatorId is for ctx.TargetOperator, but caller's linked operator is different.
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberWritesOlderDay()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true);
        ctx.Request.OperatorId = ctx.CallerOperator!.Id;
        ctx.TargetOperator = ctx.CallerOperator;
        ctx.OperatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.CallerOperator.Id, ctx.BranchUser.BranchId)
            .Returns(ctx.CallerOperator);
        var olderDay = TodayDate.AddDays(-3);
        ctx.Request.Date = olderDay;
        ctx.BranchClock.IsSameLocalDay(olderDay, FixedNow).Returns(false);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_WRITE_REQUIRES_SAME_DAY);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberSubmitsNonPresentStatus()
    {
        var ctx = BuildContext(Role.Member, linkedOperator: true);
        ctx.Request.OperatorId = ctx.CallerOperator!.Id;
        ctx.TargetOperator = ctx.CallerOperator;
        ctx.OperatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.CallerOperator.Id, ctx.BranchUser.BranchId)
            .Returns(ctx.CallerOperator);
        ctx.Request.Status = TimeEntryStatus.DayOff;
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_MEMBER_ONLY_PRESENT);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    // ── Status-relative validation ───────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenPresentMissingClockIn()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_PRESENT_REQUIRES_CLOCK_IN);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    // ── Phase 3.5 partial Present + live-running ─────────────────────────

    [Fact]
    public async Task Execute_ShouldSucceed_WhenPresentWithClockInOnly()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.ClockIn = new TimeOnly(8, 0);
        ctx.Request.ClockOut = null;

        var branchLocalNow = new DateTime(2026, 5, 5, 9, 30, 0);
        ctx.BranchClock.LocalBusinessDateTime(FixedNow).Returns(branchLocalNow);
        ctx.CalculationService.CalculateLiveRunning(
            Arg.Any<TimeOnly>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>()
        ).Returns((1.5m, -5.83m));

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.IsInProgress.ShouldBeTrue();
        response.TotalHours.ShouldBe(1.5m);
        response.BalanceHours.ShouldBe(-5.83m);
        response.ClockIn.ShouldBe(new TimeOnly(8, 0));
        response.ClockOut.ShouldBeNull();

        ctx.CalculationService.Received(1).CalculateLiveRunning(
            new TimeOnly(8, 0),
            ctx.Request.Date,
            branchLocalNow,
            ctx.Setting.DailyTargetHours,
            ctx.Setting.LunchDeductionOver6H,
            ctx.Setting.LunchDeductionOver4H);
        ctx.CalculationService.DidNotReceive().Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>());

        await ctx.TimeEntriesRepository.Received(1).Add(Arg.Is<TimeEntry>(t =>
            t.ClockIn == new TimeOnly(8, 0) &&
            t.ClockOut == null &&
            t.Status == TimeEntryStatus.Present));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenClockOutSuppliedWithoutClockIn()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = new TimeOnly(17, 0);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_CLOCK_OUT_REQUIRES_CLOCK_IN);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateInPlaceAndComputeFinalHours_WhenSecondTapClocksOut()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        var existing = new TimeEntryBuilder()
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithOperatorId(ctx.TargetOperator.Id)
            .WithDate(ctx.Request.Date)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(null)
            .WithTotalHours(0m)
            .WithBalanceHours(-7.33m)
            .Build();
        ctx.TimeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(ctx.BranchUser.BranchId, ctx.TargetOperator.Id, ctx.Request.Date)
            .Returns(existing);

        ctx.Request.ClockIn = new TimeOnly(8, 0);
        ctx.Request.ClockOut = new TimeOnly(17, 0);
        ctx.CalculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>()
        ).Returns((8m, 0.67m));

        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Id.ShouldBe(existing.Id);
        response.IsInProgress.ShouldBeFalse();
        response.TotalHours.ShouldBe(8m);
        response.BalanceHours.ShouldBe(0.67m);
        response.ClockOut.ShouldBe(new TimeOnly(17, 0));

        ctx.CalculationService.Received(1).Calculate(
            TimeEntryStatus.Present,
            new TimeOnly(8, 0),
            new TimeOnly(17, 0),
            ctx.Setting.DailyTargetHours,
            ctx.Setting.LunchDeductionOver6H,
            ctx.Setting.LunchDeductionOver4H);
        ctx.CalculationService.DidNotReceive().CalculateLiveRunning(
            Arg.Any<TimeOnly>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>());

        await ctx.TimeEntriesRepository.DidNotReceive().Add(Arg.Any<TimeEntry>());
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenNonPresentHasClocks()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.Status = TimeEntryStatus.Vacation;
        // ClockIn / ClockOut still present from default builder
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_CLOCKS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenSundayStatusOnNonSundayDate()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        // 2026-05-05 is a Tuesday; Sunday status must reject.
        ctx.Request.Status = TimeEntryStatus.Sunday;
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldSucceed_WhenSundayStatusOnSundayDate()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        var sundayDate = new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc); // Sunday
        ctx.Request.Date = sundayDate;
        ctx.Request.Status = TimeEntryStatus.Sunday;
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(TimeEntryStatus.Sunday);
        response.TotalHours.ShouldBe(7.33m); // DailyTarget for abonado
        response.BalanceHours.ShouldBe(0m);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenHolidayStatusButNoActiveHoliday()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.Status = TimeEntryStatus.Holiday;
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        ctx.HolidaysRepository
            .GetActiveByBranchIdAndDate(ctx.BranchUser.BranchId, ctx.Request.Date)
            .Returns((Holiday?)null);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldSucceed_WhenHolidayStatusAndActiveHolidayExists()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.Status = TimeEntryStatus.Holiday;
        ctx.Request.ClockIn = null;
        ctx.Request.ClockOut = null;
        var holiday = new HolidayBuilder()
            .WithBranchId(ctx.BranchUser.BranchId)
            .WithDate(ctx.Request.Date)
            .Build();
        ctx.HolidaysRepository
            .GetActiveByBranchIdAndDate(ctx.BranchUser.BranchId, ctx.Request.Date)
            .Returns(holiday);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Request);

        response.Status.ShouldBe(TimeEntryStatus.Holiday);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    // ── Branch isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTargetOperatorNotFoundInBranch()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.OperatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.OperatorId, ctx.BranchUser.BranchId)
            .Returns((Operator?)null);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.Request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_OPERATOR_NOT_FOUND);
        await ctx.OperatorsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.OperatorId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    // ── Exact-arg repository assertions ──────────────────────────────────

    [Fact]
    public async Task Execute_ShouldCallRepositoriesWithBranchScopedArgs()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        var useCase = CreateUseCase(ctx);

        await useCase.Execute(ctx.Request);

        await ctx.OperatorsRepository.Received(1)
            .GetActiveByIdAndBranchIdAsNoTracking(ctx.Request.OperatorId, ctx.BranchUser.BranchId);
        await ctx.TimeEntriesRepository.Received(1)
            .GetByBranchIdOperatorIdAndDate(ctx.BranchUser.BranchId, ctx.Request.OperatorId, ctx.Request.Date);
        await ctx.SettingsRepository.Received(1)
            .GetByBranchIdAsNoTracking(ctx.BranchUser.BranchId);
    }

    // ── Validator failures (shape) ───────────────────────────────────────

    [Fact]
    public async Task Execute_ShouldThrowOnValidation_WhenOperatorIdEmpty()
    {
        var ctx = BuildContext(Role.Manager, linkedOperator: false);
        ctx.Request.OperatorId = Guid.Empty;
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPERATOR_ID_REQUIRED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    // ── Test infrastructure ──────────────────────────────────────────────

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; set; }
        public required Operator? CallerOperator { get; set; }
        public required Operator TargetOperator { get; set; }
        public required RequestUpsertTimeEntryJson Request { get; set; }
        public required Setting Setting { get; set; }
        public required IAuthenticationService AuthenticationService { get; set; }
        public required IOperatorsRepository OperatorsRepository { get; set; }
        public required IOperatorAccountsRepository OperatorAccountsRepository { get; set; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; set; }
        public required ISettingsRepository SettingsRepository { get; set; }
        public required IHolidaysRepository HolidaysRepository { get; set; }
        public required ITimeEntryCalculationService CalculationService { get; set; }
        public required IBranchClock BranchClock { get; set; }
        public required IUnitOfWork UnitOfWork { get; set; }
    }

    private static TestContext BuildContext(Role role, bool linkedOperator)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var callerOperator = linkedOperator
            ? new OperatorBuilder()
                .WithBranchId(branchUser.BranchId)
                .WithUserId(branchUser.UserId)
                .Build()
            : null;
        // Target operator is a different operator from the Member's own; tests that need
        // the same operator overwrite this explicitly (Member self-same-day-Present case).
        var targetOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Lenna Doe")
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(targetOperator.Id)
            .WithDate(TodayDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithClockIn(new TimeOnly(8, 0))
            .WithClockOut(new TimeOnly(17, 0))
            .Build();
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            BranchId = branchUser.BranchId,
            DailyTargetHours = 7.33m,
            LunchDeductionOver6H = 1.0m,
            LunchDeductionOver4H = 0.25m
        };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var operatorsRepository = Substitute.For<IOperatorsRepository>();
        operatorsRepository
            .GetActiveLinkedByUserIdAndBranchIdAsNoTracking(branchUser.UserId, branchUser.BranchId)
            .Returns(callerOperator);
        operatorsRepository
            .GetActiveByIdAndBranchIdAsNoTracking(targetOperator.Id, branchUser.BranchId)
            .Returns(targetOperator);
        var operatorAccountsRepository = Substitute.For<IOperatorAccountsRepository>();
        operatorAccountsRepository
            .ListActiveByOperatorIdAsNoTracking(Arg.Any<Guid>())
            .Returns([]);
        var timeEntriesRepository = Substitute.For<ITimeEntriesRepository>();
        timeEntriesRepository
            .GetByBranchIdOperatorIdAndDate(branchUser.BranchId, targetOperator.Id, request.Date)
            .Returns((TimeEntry?)null);
        var settingsRepository = Substitute.For<ISettingsRepository>();
        settingsRepository
            .GetByBranchIdAsNoTracking(branchUser.BranchId)
            .Returns(setting);
        var holidaysRepository = Substitute.For<IHolidaysRepository>();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedNow);
        branchClock.IsSameLocalDay(Arg.Any<DateTime>(), Arg.Any<DateTime>()).Returns(true);
        // Default LocalBusinessDateTime stub; tests that exercise live-running override it.
        branchClock.LocalBusinessDateTime(Arg.Any<DateTime>()).Returns(c => ((DateTime)c[0]).Date);
        // Substitute the calc service but delegate to the real implementation by default so
        // existing tests that exercise calc behavior keep their realistic results. Live-running
        // and exact-arg tests override the relevant stubs explicitly.
        var realCalculationService = new TimeEntryCalculationService();
        var calculationService = Substitute.For<ITimeEntryCalculationService>();
        calculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<TimeOnly?>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>()
        ).Returns(c => realCalculationService.Calculate(
            (TimeEntryStatus)c[0],
            (TimeOnly?)c[1],
            (TimeOnly?)c[2],
            (decimal)c[3],
            (decimal)c[4],
            (decimal)c[5]));
        calculationService.CalculateLiveRunning(
            Arg.Any<TimeOnly>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>()
        ).Returns(c => realCalculationService.CalculateLiveRunning(
            (TimeOnly)c[0],
            (DateTime)c[1],
            (DateTime)c[2],
            (decimal)c[3],
            (decimal)c[4],
            (decimal)c[5]));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new TestContext
        {
            BranchUser = branchUser,
            CallerOperator = callerOperator,
            TargetOperator = targetOperator,
            Request = request,
            Setting = setting,
            AuthenticationService = authenticationService,
            OperatorsRepository = operatorsRepository,
            OperatorAccountsRepository = operatorAccountsRepository,
            TimeEntriesRepository = timeEntriesRepository,
            SettingsRepository = settingsRepository,
            HolidaysRepository = holidaysRepository,
            CalculationService = calculationService,
            BranchClock = branchClock,
            UnitOfWork = unitOfWork
        };
    }

    private static UpsertTimeEntryUseCase CreateUseCase(TestContext ctx)
    {
        var memberAccountScopeResolver = new MemberAccountScopeResolver(
            ctx.OperatorsRepository,
            ctx.OperatorAccountsRepository);
        var permissionGuard = new TimeEntryWritePermissionGuard(ctx.BranchClock);

        return new UpsertTimeEntryUseCase(
            ctx.AuthenticationService,
            memberAccountScopeResolver,
            ctx.OperatorsRepository,
            ctx.TimeEntriesRepository,
            ctx.SettingsRepository,
            ctx.HolidaysRepository,
            permissionGuard,
            ctx.CalculationService,
            ctx.BranchClock,
            ctx.UnitOfWork);
    }
}
