using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.AddSegment;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TimeEntries.AddSegment;

public class AddTimeEntrySegmentUseCaseTest
{
    private static readonly DateTime FixedUtcNow = new(2026, 5, 8, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime EntryDate = new(2026, 5, 8);

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin", Role.Admin },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public async Task Execute_ShouldAddSegmentAndRecalculateTotals_WhenElevatedRoleSubmitsValidClocks(string _, Role role)
    {
        var ctx = BuildContext(role);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(EntryDate.AddHours(17))
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.Parent.Id, request);

        response.Id.ShouldBe(ctx.Parent.Id);
        response.Segments.Count.ShouldBe(2);
        response.TotalHours.ShouldBe(8m, tolerance: 0.001m);
        response.BalanceHours.ShouldBe(8m - ctx.Setting.DailyTargetHours, tolerance: 0.001m);
        ctx.Parent.UpdatedAt.ShouldBe(FixedUtcNow);
        ctx.Parent.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        await ctx.TimeEntriesRepository.Received(1).GetByIdAndBranchId(ctx.Parent.Id, ctx.BranchUser.BranchId);
        await ctx.TimeEntrySegmentsRepository.Received(1).Add(Arg.Is<TimeEntrySegment>(segment =>
            segment.TimeEntryId == ctx.Parent.Id &&
            segment.ClockIn == EntryDate.AddHours(13) &&
            segment.ClockOut == EntryDate.AddHours(17)));
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberAttempts()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.Parent.Id, new RequestAddTimeEntrySegmentJsonBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await ctx.TimeEntriesRepository.DidNotReceive().GetByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenTimeEntryIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, parentMissing: true);
        var missingId = Guid.NewGuid();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(missingId, new RequestAddTimeEntrySegmentJsonBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
        await ctx.TimeEntriesRepository.Received(1).GetByIdAndBranchId(missingId, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenParentStatusIsNotPresent()
    {
        var ctx = BuildContext(Role.Admin, status: TimeEntryStatus.Vacation);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(ctx.Parent.Id, new RequestAddTimeEntrySegmentJsonBuilder().Build()));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenNewSegmentOverlapsExisting()
    {
        var ctx = BuildContext(Role.Manager);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(11))
            .WithClockOut(EntryDate.AddHours(17))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Parent.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().Add(Arg.Any<TimeEntrySegment>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenNewSegmentIsOutOfDayBounds()
    {
        var ctx = BuildContext(Role.Admin);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddDays(1))
            .WithClockOut(EntryDate.AddDays(1).AddHours(1))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Parent.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().Add(Arg.Any<TimeEntrySegment>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenAddingOpenSegmentAndOneAlreadyExists()
    {
        var ctx = BuildContext(Role.Manager, existingOpen: true);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Parent.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().Add(Arg.Any<TimeEntrySegment>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenClockOutIsBeforeClockIn()
    {
        var ctx = BuildContext(Role.Admin);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(12))
            .WithClockOut(EntryDate.AddHours(11))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.Parent.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
        await ctx.TimeEntriesRepository.DidNotReceive().GetByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksEntryDate()
    {
        var ctx = BuildContext(Role.Manager, lockDate: EntryDate);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(EntryDate.AddHours(17))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.Parent.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().Add(Arg.Any<TimeEntrySegment>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required TimeEntry Parent { get; init; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITimeEntriesRepository TimeEntriesRepository { get; init; }
        public required ITimeEntrySegmentsRepository TimeEntrySegmentsRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required ITimeEntryPoliciesRepository TimeEntryPoliciesRepository { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }

    private static TestContext BuildContext(
        Role role,
        TimeEntryStatus status = TimeEntryStatus.Present,
        DateTime? existingClockOut = null,
        DateTime? lockDate = null,
        bool parentMissing = false,
        bool existingOpen = false)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Lenna").Build();
        var parent = new TimeEntryBuilder()
            .WithOperator(op)
            .WithDate(EntryDate)
            .WithStatus(status)
            .Build();
        parent.Segments.Add(Segment(
            parent,
            EntryDate.AddHours(8),
            existingOpen ? null : existingClockOut ?? EntryDate.AddHours(12)));

        var setting = new Setting
        {
            Id = Guid.NewGuid(),
            BranchId = branchUser.BranchId,
            LockDate = lockDate ?? DateTime.MinValue,
            DailyTargetHours = 7.33m,
            LunchDeductionOver6H = 1m,
            LunchDeductionOver4H = 0.25m
        };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(parent.Id, branchUser.BranchId, parentMissing ? null : parent)
            .Build();
        var timeEntrySegmentsRepository = new TimeEntrySegmentsRepositoryBuilder().Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();
        var timeEntryPoliciesRepository = new TimeEntryPoliciesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTrackingReturns(branchUser.BranchId, [
                new TimeEntryPolicyBuilder()
                    .WithBranchId(branchUser.BranchId)
                    .WithDailyTargetHours(setting.DailyTargetHours)
                    .WithLunchDeductionOver6H(setting.LunchDeductionOver6H)
                    .WithLunchDeductionOver4H(setting.LunchDeductionOver4H)
                    .Build()
            ])
            .Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(FixedUtcNow).Returns(EntryDate.AddHours(18));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new TestContext
        {
            BranchUser = branchUser,
            Parent = parent,
            Setting = setting,
            AuthenticationService = authenticationService,
            TimeEntriesRepository = timeEntriesRepository,
            TimeEntrySegmentsRepository = timeEntrySegmentsRepository,
            SettingsRepository = settingsRepository,
            TimeEntryPoliciesRepository = timeEntryPoliciesRepository,
            BranchClock = branchClock,
            UnitOfWork = unitOfWork
        };
    }

    private static AddTimeEntrySegmentUseCase CreateUseCase(TestContext ctx)
    {
        return new AddTimeEntrySegmentUseCase(
            ctx.AuthenticationService,
            ctx.TimeEntriesRepository,
            ctx.TimeEntrySegmentsRepository,
            new LockDateGuard(new LockDateReader(ctx.SettingsRepository)),
            new TimeEntrySegmentMutationService(ctx.TimeEntryPoliciesRepository, new TimeEntryCalculationService()),
            ctx.BranchClock,
            new MonthLockCoordinationBuilder().Build(),
            ctx.UnitOfWork);
    }

    private static TimeEntrySegment Segment(TimeEntry parent, DateTime clockIn, DateTime? clockOut)
    {
        return new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            CreatedAt = FixedUtcNow.AddHours(-1),
            TimeEntryId = parent.Id,
            TimeEntry = parent,
            ClockIn = clockIn,
            ClockOut = clockOut
        };
    }
}
