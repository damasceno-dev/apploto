using CommonTestUtilities.Entities;
using NSubstitute;
using server.Application.Services.TimeEntries;
using server.Application.Services.Transactions;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.TimeEntries;

public class TimeEntryWritePermissionGuardTest
{
    private static readonly DateTime StandardUtcNow = new(2026, 5, 5, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime TargetDate = new(2026, 5, 5, 0, 0, 0, DateTimeKind.Utc);

    // ── Elevated roles ────────────────────────────────────────────────────

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin",   Role.Admin   },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public void Evaluate_ElevatedRole_ReturnsElevated_RegardlessOfOperatorOrDayOrStatus(string _,Role role)
    {
        var caller = new BranchUserBuilder().WithRole(role).Build();
        var branchClock = Substitute.For<IBranchClock>();
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator: null,
            targetOperatorId: Guid.NewGuid(),
            targetDate: TargetDate,
            status: TimeEntryStatus.DayOff,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.Elevated);
        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    // ── Member success ────────────────────────────────────────────────────

    [Fact]
    public void Evaluate_MemberOwnOperatorSameDayPresent_ReturnsSelfSameDayPresent()
    {
        var callerOperator = new OperatorBuilder().Build();
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(TargetDate, StandardUtcNow).Returns(true);
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator,
            targetOperatorId: callerOperator.Id,
            targetDate: TargetDate,
            status: TimeEntryStatus.Present,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.SelfSameDayPresent);
        branchClock.Received(1).IsSameLocalDay(TargetDate, StandardUtcNow);
    }

    // ── Member denial — missing linked operator ───────────────────────────

    [Fact]
    public void Evaluate_MemberNoLinkedOperator_ReturnsMissingLinkedOperator()
    {
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator: null,
            targetOperatorId: Guid.NewGuid(),
            targetDate: TargetDate,
            status: TimeEntryStatus.Present,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.MissingLinkedOperator);
        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    // ── Member denial — targeting another operator ────────────────────────

    [Fact]
    public void Evaluate_MemberLinkedButTargetingAnotherOperator_ReturnsNotOwnOperator()
    {
        var callerOperator = new OperatorBuilder().Build();
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator,
            targetOperatorId: Guid.NewGuid(),
            targetDate: TargetDate,
            status: TimeEntryStatus.Present,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.NotOwnOperator);
        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    // ── Member denial — older day ─────────────────────────────────────────

    [Fact]
    public void Evaluate_MemberOwnOperatorOlderDay_ReturnsOlderDayMember()
    {
        var callerOperator = new OperatorBuilder().Build();
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(TargetDate, StandardUtcNow).Returns(false);
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator,
            targetOperatorId: callerOperator.Id,
            targetDate: TargetDate,
            status: TimeEntryStatus.Present,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.OlderDayMember);
        branchClock.Received(1).IsSameLocalDay(TargetDate, StandardUtcNow);
    }

    // ── Member denial — non-Present status ───────────────────────────────

    public static TheoryData<string, TimeEntryStatus> NonPresentStatuses => new()
    {
        { "DayOff",             TimeEntryStatus.DayOff },
        { "Sunday",             TimeEntryStatus.Sunday },
        { "Holiday",            TimeEntryStatus.Holiday },
        { "Vacation",           TimeEntryStatus.Vacation },
        { "JustifiedAbsence",   TimeEntryStatus.JustifiedAbsence },
        { "UnjustifiedAbsence", TimeEntryStatus.UnjustifiedAbsence },
    };

    [Theory]
    [MemberData(nameof(NonPresentStatuses))]
    public void Evaluate_MemberOwnOperatorSameDay_NonPresentStatus_ReturnsMemberNonPresent(
        string _,
        TimeEntryStatus status)
    {
        var callerOperator = new OperatorBuilder().Build();
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(TargetDate, StandardUtcNow).Returns(true);
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator,
            targetOperatorId: callerOperator.Id,
            targetDate: TargetDate,
            status: status,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.MemberNonPresent);
    }

    // ── Evaluation order: OlderDay is checked before NonPresent ──────────

    [Fact]
    public void Evaluate_MemberOwnOperatorOlderDay_NonPresentStatus_ReturnsOlderDayMember_NotMemberNonPresent()
    {
        var callerOperator = new OperatorBuilder().Build();
        var caller = new BranchUserBuilder().WithRole(Role.Member).Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(TargetDate, StandardUtcNow).Returns(false);
        var guard = new TimeEntryWritePermissionGuard(branchClock);

        var outcome = guard.Evaluate(
            caller,
            callerOperator,
            targetOperatorId: callerOperator.Id,
            targetDate: TargetDate,
            status: TimeEntryStatus.DayOff,
            utcNow: StandardUtcNow);

        outcome.ShouldBe(TimeEntryWritePermissionOutcome.OlderDayMember);
    }
}
