using CommonTestUtilities.Entities;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.UpdateSegment;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.UpdateSegment;

public class UpdateTimeEntrySegmentUseCaseTest
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
    public async Task Execute_ShouldUpdateSegmentAndRecalculateTotals_WhenElevatedRoleSubmitsValidClocks(string _, Role role)
    {
        var ctx = BuildContext(role);
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(12.5))
            .WithClockOut(EntryDate.AddHours(17.5))
            .Build();
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.TargetSegment.Id, request);

        ctx.TargetSegment.ClockIn.ShouldBe(EntryDate.AddHours(12.5));
        ctx.TargetSegment.ClockOut.ShouldBe(EntryDate.AddHours(17.5));
        ctx.TargetSegment.UpdatedAt.ShouldBe(FixedUtcNow);
        ctx.TargetSegment.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.Parent.UpdatedAt.ShouldBe(FixedUtcNow);
        ctx.Parent.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        response.TotalHours.ShouldBe(8.5m, tolerance: 0.001m);
        response.BalanceHours.ShouldBe(8.5m - ctx.Setting.DailyTargetHours, tolerance: 0.001m);
        await ctx.TimeEntrySegmentsRepository.Received(1)
            .GetActiveByIdAndBranchId(ctx.TargetSegment.Id, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberAttempts()
    {
        var ctx = BuildContext(Role.Member);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(ctx.TargetSegment.Id, new RequestUpdateTimeEntrySegmentJsonBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenSegmentIsMissingOrCrossBranch()
    {
        var ctx = BuildContext(Role.Manager, segmentMissing: true);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(ctx.TargetSegment.Id, new RequestUpdateTimeEntrySegmentJsonBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
        await ctx.TimeEntrySegmentsRepository.Received(1)
            .GetActiveByIdAndBranchId(ctx.TargetSegment.Id, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenParentStatusIsNotPresent()
    {
        var ctx = BuildContext(Role.Admin, status: TimeEntryStatus.Vacation);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(ctx.TargetSegment.Id, new RequestUpdateTimeEntrySegmentJsonBuilder().Build()));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_NON_PRESENT_REJECTS_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenUpdatedSegmentOverlapsSibling()
    {
        var ctx = BuildContext(Role.Manager);
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(11))
            .WithClockOut(EntryDate.AddHours(17))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.TargetSegment.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenUpdatedSegmentIsOutOfDayBounds()
    {
        var ctx = BuildContext(Role.Admin);
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddDays(1))
            .WithClockOut(EntryDate.AddDays(1).AddHours(1))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.TargetSegment.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenUpdateWouldCreateMultipleOpenSegments()
    {
        var ctx = BuildContext(Role.Manager, siblingOpen: true);
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(13))
            .WithClockOut(null)
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.TargetSegment.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenClockOutIsBeforeClockIn()
    {
        var ctx = BuildContext(Role.Admin);
        var request = new RequestUpdateTimeEntrySegmentJsonBuilder()
            .WithClockIn(EntryDate.AddHours(12))
            .WithClockOut(EntryDate.AddHours(11))
            .Build();
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(ctx.TargetSegment.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksEntryDate()
    {
        var ctx = BuildContext(Role.Manager, lockDate: EntryDate);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => useCase.Execute(ctx.TargetSegment.Id, new RequestUpdateTimeEntrySegmentJsonBuilder().Build()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    private sealed class TestContext
    {
        public required BranchUser BranchUser { get; init; }
        public required TimeEntry Parent { get; init; }
        public required TimeEntrySegment TargetSegment { get; init; }
        public required Setting Setting { get; init; }
        public required IAuthenticationService AuthenticationService { get; init; }
        public required ITimeEntrySegmentsRepository TimeEntrySegmentsRepository { get; init; }
        public required ISettingsRepository SettingsRepository { get; init; }
        public required IBranchClock BranchClock { get; init; }
        public required IUnitOfWork UnitOfWork { get; init; }
    }

    private static TestContext BuildContext(
        Role role,
        TimeEntryStatus status = TimeEntryStatus.Present,
        DateTime? lockDate = null,
        bool segmentMissing = false,
        bool siblingOpen = false)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Lenna").Build();
        var parent = new TimeEntryBuilder()
            .WithOperator(op)
            .WithDate(EntryDate)
            .WithStatus(status)
            .Build();
        parent.Segments.Add(Segment(parent, EntryDate.AddHours(8), EntryDate.AddHours(12)));
        var target = Segment(parent, EntryDate.AddHours(13), EntryDate.AddHours(17));
        parent.Segments.Add(target);

        if (siblingOpen)
            parent.Segments.Add(Segment(parent, EntryDate.AddHours(18), null));

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
        var timeEntrySegmentsRepository = new TimeEntrySegmentsRepositoryBuilder()
            .GetActiveByIdAndBranchIdReturns(target.Id, branchUser.BranchId, segmentMissing ? null : target)
            .Build();
        var settingsRepository = new SettingsRepositoryBuilder()
            .GetByBranchIdAsNoTrackingReturns(branchUser.BranchId, setting)
            .Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedUtcNow);
        branchClock.LocalBusinessDateTime(FixedUtcNow).Returns(EntryDate.AddHours(18));
        var unitOfWork = new UnitOfWorkBuilder().Build();

        return new TestContext
        {
            BranchUser = branchUser,
            Parent = parent,
            TargetSegment = target,
            Setting = setting,
            AuthenticationService = authenticationService,
            TimeEntrySegmentsRepository = timeEntrySegmentsRepository,
            SettingsRepository = settingsRepository,
            BranchClock = branchClock,
            UnitOfWork = unitOfWork
        };
    }

    private static UpdateTimeEntrySegmentUseCase CreateUseCase(TestContext ctx)
    {
        return new UpdateTimeEntrySegmentUseCase(
            ctx.AuthenticationService,
            ctx.TimeEntrySegmentsRepository,
            new LockDateGuard(ctx.SettingsRepository),
            new TimeEntrySegmentMutationService(ctx.SettingsRepository, new TimeEntryCalculationService()),
            ctx.BranchClock,
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
