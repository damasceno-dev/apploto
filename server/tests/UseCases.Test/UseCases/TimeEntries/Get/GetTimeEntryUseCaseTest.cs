using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Members;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Get;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.Get;

public class GetTimeEntryUseCaseTest
{
    private static readonly DateTime EntryDate = new(2026, 5, 8);
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 20, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime BranchLocalNow = new(2026, 5, 8, 17, 0, 0);

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenEntryMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Admin, existing: null);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid()));
        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberWithoutLinkedOperator()
    {
        var entry = BuildEntry(closedSegment: true);
        var ctx = BuildContext(Role.Member, existing: entry, linkedOperator: null);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(entry.Id));
        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task Execute_ShouldThrowForbidden_WhenMemberTargetsAnotherOperatorsEntry()
    {
        var otherOperator = new OperatorBuilder().Build();
        var entry = BuildEntry(closedSegment: true, op: otherOperator);
        var linkedOperator = new OperatorBuilder().WithBranchId(otherOperator.BranchId).Build();
        var ctx = BuildContext(Role.Member, existing: entry, linkedOperator: linkedOperator);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(entry.Id));
        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_OWN_OPERATOR);
    }

    [Fact]
    public async Task Execute_ShouldReturnEntry_WhenManagerReadsAnyEntry()
    {
        var entry = BuildEntry(closedSegment: true);
        var ctx = BuildContext(Role.Manager, existing: entry);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(entry.Id);

        response.Id.ShouldBe(entry.Id);
        response.IsInProgress.ShouldBeFalse();
        response.TotalHours.ShouldBe(entry.TotalHours);
        response.BalanceHours.ShouldBe(entry.BalanceHours);
        response.Segments.Count.ShouldBe(1);
        ctx.CalculationService.DidNotReceive().Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>());
    }

    [Fact]
    public async Task Execute_ShouldRecomputeLiveRunning_ForInProgressCurrentDayEntry()
    {
        var entry = BuildEntry();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(null)
            .Build());
        entry.TotalHours = 0m;
        entry.BalanceHours = 0m;

        var ctx = BuildContext(Role.Admin, existing: entry);
        ctx.CalculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>())
            .Returns((9m, 1.67m));

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(entry.Id);

        response.IsInProgress.ShouldBeTrue();
        response.TotalHours.ShouldBe(9m);
        response.BalanceHours.ShouldBe(1.67m);
        ctx.CalculationService.Received(1).Calculate(
            entry.Status,
            Arg.Is<IReadOnlyList<TimeEntrySegmentInput>>(list => list.Count == 1 && list[0].ClockOut == null),
            entry.Date,
            BranchLocalNow,
            ctx.Setting.DailyTargetHours,
            ctx.Setting.LunchDeductionOver6H,
            ctx.Setting.LunchDeductionOver4H);
    }

    [Fact]
    public async Task Execute_ShouldRecomputeAndReportInProgress_ForForgottenPriorDayOpenWithCompletedSibling()
    {
        var entry = BuildEntry();
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(EntryDate.AddHours(8))
            .WithClockOut(EntryDate.AddHours(12))
            .Build());
        entry.Segments.Add(new TimeEntrySegmentBuilder()
            .WithTimeEntry(entry)
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(null)
            .Build());
        entry.TotalHours = 4m;
        entry.BalanceHours = -3.33m;

        var ctx = BuildContext(Role.Admin, existing: entry);
        ctx.CalculationService.Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>())
            .Returns((4m, -3.33m));

        var useCase = CreateUseCase(ctx);
        var response = await useCase.Execute(entry.Id);

        response.IsInProgress.ShouldBeTrue();
        response.TotalHours.ShouldBe(4m);
        response.BalanceHours.ShouldBe(-3.33m);
        response.Segments.Count.ShouldBe(2);
        ctx.CalculationService.Received(1).Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Is<IReadOnlyList<TimeEntrySegmentInput>>(list => list.Count == 2),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>());
    }

    [Fact]
    public async Task Execute_ShouldPassThroughPersistedTotals_ForFullyClosedShift()
    {
        var entry = BuildEntry(closedSegment: true);
        var ctx = BuildContext(Role.Admin, existing: entry);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(entry.Id);

        response.IsInProgress.ShouldBeFalse();
        response.TotalHours.ShouldBe(entry.TotalHours);
        response.BalanceHours.ShouldBe(entry.BalanceHours);
        ctx.CalculationService.DidNotReceive().Calculate(
            Arg.Any<TimeEntryStatus>(),
            Arg.Any<IReadOnlyList<TimeEntrySegmentInput>>(),
            Arg.Any<DateTime>(),
            Arg.Any<DateTime>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>(),
            Arg.Any<decimal>());
    }

    [Fact]
    public async Task Execute_ShouldReturnEntry_WhenMemberReadsOwnOperatorsEntry()
    {
        var linkedOperator = new OperatorBuilder().Build();
        var entry = BuildEntry(closedSegment: true, op: linkedOperator);
        var ctx = BuildContext(Role.Member, existing: entry, linkedOperator: linkedOperator);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(entry.Id);

        response.Id.ShouldBe(entry.Id);
        response.OperatorId.ShouldBe(linkedOperator.Id);
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required Operator? LinkedOperator { get; init; }
        public required TimeEntry? Existing { get; init; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required IMemberAccountScopeResolver MemberScopeResolver { get; init; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required ITimeEntryCalculationService CalculationService { get; init; }
        public required IBranchClock BranchClock { get; init; }
    }

    private static TestContext BuildContext(
        Role role,
        TimeEntry? existing = null,
        Operator? linkedOperator = null)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            BranchId = branchUser.BranchId,
            DailyTargetHours = 7.33m,
            LunchDeductionOver6H = 1m,
            LunchDeductionOver4H = 0.25m
        };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();

        var memberScopeResolver = Substitute.For<IMemberAccountScopeResolver>();
        memberScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId)
            .Returns(new MemberAccountScope(linkedOperator, linkedOperator is null ? [] : [Guid.NewGuid()]));

        var timeEntriesRepository = Substitute.For<ITimeEntriesRepository>();
        timeEntriesRepository.GetByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), branchUser.BranchId)
            .Returns(existing);

        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();

        var calculationService = Substitute.For<ITimeEntryCalculationService>();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(FixedUtcNow).Returns(BranchLocalNow);

        return new TestContext
        {
            BranchUser = branchUser,
            LinkedOperator = linkedOperator,
            Existing = existing,
            Setting = setting,
            AuthenticationService = authenticationService,
            MemberScopeResolver = memberScopeResolver,
            TimeEntriesRepository = timeEntriesRepository,
            SettingsRepository = settingsRepository,
            CalculationService = calculationService,
            BranchClock = branchClock
        };
    }

    private static GetTimeEntryUseCase CreateUseCase(TestContext ctx)
    {
        return new GetTimeEntryUseCase(
            ctx.AuthenticationService,
            ctx.MemberScopeResolver,
            ctx.TimeEntriesRepository,
            ctx.SettingsRepository,
            ctx.CalculationService,
            ctx.BranchClock);
    }

    private static TimeEntry BuildEntry(bool closedSegment = false, Operator? op = null)
    {
        var entry = new TimeEntryBuilder()
            .WithDate(EntryDate)
            .WithStatus(TimeEntryStatus.Present)
            .WithTotalHours(8m)
            .WithBalanceHours(0.67m)
            .WithOperator(op ?? new OperatorBuilder().WithName("Lenna").Build())
            .Build();

        if (closedSegment)
        {
            entry.Segments.Add(new TimeEntrySegmentBuilder()
                .WithTimeEntry(entry)
                .WithClockIn(EntryDate.AddHours(8))
                .WithClockOut(EntryDate.AddHours(17))
                .Build());
        }

        return entry;
    }
}
