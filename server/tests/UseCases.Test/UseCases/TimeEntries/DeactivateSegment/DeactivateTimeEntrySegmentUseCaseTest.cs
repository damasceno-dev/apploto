using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Settings;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.DeactivateSegment;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.UseCases.TimeEntries.DeactivateSegment;

public class DeactivateTimeEntrySegmentUseCaseTest
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
    public async Task Execute_ShouldSoftDeactivateSegmentAndRecalculateParent_WhenElevatedRoleSubmitsValidId(string _, Role role)
    {
        var ctx = BuildContext(role);
        var useCase = CreateUseCase(ctx);

        var response = await useCase.Execute(ctx.TargetSegment.Id);

        ctx.TargetSegment.Active.ShouldBeFalse();
        ctx.TargetSegment.UpdatedAt.ShouldBe(FixedUtcNow);
        ctx.TargetSegment.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.Parent.UpdatedAt.ShouldBe(FixedUtcNow);
        ctx.Parent.UpdatedByUserId.ShouldBe(ctx.BranchUser.UserId);
        ctx.Parent.TotalHours.ShouldBe(0m);
        ctx.Parent.BalanceHours.ShouldBe(-ctx.Setting.DailyTargetHours);
        response.Segments.ShouldBeEmpty();
        response.IsInProgress.ShouldBeFalse();
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
            () => useCase.Execute(ctx.TargetSegment.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await ctx.TimeEntrySegmentsRepository.DidNotReceive().GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenSegmentIsMissingCrossBranchOrAlreadyInactive()
    {
        var ctx = BuildContext(Role.Admin, segmentMissing: true);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(ctx.TargetSegment.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_SEGMENT_NOT_FOUND);
        await ctx.TimeEntrySegmentsRepository.Received(1)
            .GetActiveByIdAndBranchId(ctx.TargetSegment.Id, ctx.BranchUser.BranchId);
        await ctx.UnitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflict_WhenLockDateBlocksEntryDate()
    {
        var ctx = BuildContext(Role.Manager, lockDate: EntryDate);
        var useCase = CreateUseCase(ctx);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(ctx.TargetSegment.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_DATE_LOCKED);
        ctx.TargetSegment.Active.ShouldBeTrue();
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
        DateTime? lockDate = null,
        bool segmentMissing = false)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Lenna").Build();
        var parent = new TimeEntryBuilder()
            .WithOperator(op)
            .WithDate(EntryDate)
            .WithStatus(TimeEntryStatus.Present)
            .Build();
        var target = Segment(parent, EntryDate.AddHours(8), EntryDate.AddHours(17));
        parent.Segments.Add(target);

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

    private static DeactivateTimeEntrySegmentUseCase CreateUseCase(TestContext ctx)
    {
        return new DeactivateTimeEntrySegmentUseCase(
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
