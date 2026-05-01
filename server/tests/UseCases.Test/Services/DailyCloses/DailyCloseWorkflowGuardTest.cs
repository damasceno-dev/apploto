using CommonTestUtilities.Entities;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;
using Operator = server.Domain.Entities.Operator;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseWorkflowGuardTest
{
    public static TheoryData<DailyCloseStatus> SubmittableStatuses =>
    [
        DailyCloseStatus.Draft,
        DailyCloseStatus.Rejected
    ];

    public static TheoryData<DailyCloseStatus> NotSubmittableStatuses =>
    [
        DailyCloseStatus.Submitted,
        DailyCloseStatus.Approved
    ];

    // ──────────────────────────────────────────────
    // Draft → EditOnDraft
    // ──────────────────────────────────────────────

    [Fact]
    public void EnsureCanEditItems_ShouldReturnEditOnDraft_WhenManagerAndDraft()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(DailyCloseStatus.Draft);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator: null);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnDraft);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldReturnEditOnDraft_WhenMemberIsRecordingOperatorAndSameDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Draft, submittedByOperatorId: callerOperator.Id);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnDraft);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldThrow_WhenMemberIsOtherOperatorOnDraft()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var otherOperatorId = Guid.NewGuid();
        var close = BuildClose(DailyCloseStatus.Draft, submittedByOperatorId: otherOperatorId);
        var guard = BuildGuard(new TodayBranchClock());

        Should.Throw<ConflictException>(() => guard.EnsureCanEditItems(close, branchUser, callerOperator));
    }

    [Fact]
    public void EnsureCanEditItems_ShouldThrow_WhenMemberIsRecordingOperatorButOlderDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Draft,
            submittedByOperatorId: callerOperator.Id,
            date: DateTime.UtcNow.Date.AddDays(-1));
        var guard = BuildGuard(new TodayBranchClock());

        Should.Throw<ConflictException>(() => guard.EnsureCanEditItems(close, branchUser, callerOperator));
    }

    // ──────────────────────────────────────────────
    // Rejected → EditOnRejectedAutoTransitionToDraft
    // ──────────────────────────────────────────────

    [Fact]
    public void EnsureCanEditItems_ShouldReturnEditOnRejected_WhenManagerAndRejected()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(DailyCloseStatus.Rejected);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator: null);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldReturnEditOnRejected_WhenMemberIsRecordingOperatorAndSameDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Rejected, submittedByOperatorId: callerOperator.Id);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft);
    }

    // ──────────────────────────────────────────────
    // Submitted → EditOnSubmittedRecallToDraft
    // ──────────────────────────────────────────────

    [Fact]
    public void EnsureCanEditItems_ShouldReturnRecall_WhenMemberIsRecordingOperatorAndSameDayAndSubmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, submittedByOperatorId: callerOperator.Id);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldReturnRecall_WhenManagerAndSubmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(DailyCloseStatus.Submitted);
        var guard = BuildGuard(new TodayBranchClock());

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator: null);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldThrow_WhenMemberIsOtherOperatorAndSubmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, submittedByOperatorId: Guid.NewGuid());
        var guard = BuildGuard(new TodayBranchClock());

        Should.Throw<ConflictException>(() => guard.EnsureCanEditItems(close, branchUser, callerOperator));
    }

    [Fact]
    public void EnsureCanEditItems_ShouldThrow_WhenMemberIsRecordingOperatorButOlderDayAndSubmitted()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Submitted,
            submittedByOperatorId: callerOperator.Id,
            date: DateTime.UtcNow.Date.AddDays(-1));
        var guard = BuildGuard(new TodayBranchClock());

        Should.Throw<ConflictException>(() => guard.EnsureCanEditItems(close, branchUser, callerOperator));
    }

    // ──────────────────────────────────────────────
    // Approved → always 409
    // ──────────────────────────────────────────────

    [Fact]
    public void EnsureCanEditItems_ShouldThrow_WhenApproved()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var close = BuildClose(DailyCloseStatus.Approved);
        var guard = BuildGuard(new TodayBranchClock());

        Should.Throw<ConflictException>(() => guard.EnsureCanEditItems(close, branchUser, callerOperator: null));
    }

    // ──────────────────────────────────────────────
    // Submit
    // ──────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(SubmittableStatuses))]
    public void EnsureCanSubmit_ShouldAllowManager_WhenDraftOrRejected(DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(status, date: DateTime.UtcNow.Date.AddDays(-2));
        var guard = BuildGuard(new TodayBranchClock());

        Should.NotThrow(() => guard.EnsureCanSubmit(close, branchUser, callerOperator: null));
    }

    [Theory]
    [MemberData(nameof(SubmittableStatuses))]
    public void EnsureCanSubmit_ShouldAllowMember_WhenRecordingOperatorAndSameDay(DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(status, submittedByOperatorId: callerOperator.Id);
        var guard = BuildGuard(new TodayBranchClock());

        Should.NotThrow(() => guard.EnsureCanSubmit(close, branchUser, callerOperator));
    }

    [Theory]
    [MemberData(nameof(NotSubmittableStatuses))]
    public void EnsureCanSubmit_ShouldThrowConflict_WhenStatusIsNotDraftOrRejected(DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var close = BuildClose(status);
        var guard = BuildGuard(new TodayBranchClock());

        var exception = Should.Throw<ConflictException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator: null));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldThrow_WhenMemberHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var close = BuildClose(DailyCloseStatus.Draft);
        var guard = BuildGuard(new TodayBranchClock());

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator: null));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldThrow_WhenMemberIsNotRecordingOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Draft, submittedByOperatorId: Guid.NewGuid());
        var guard = BuildGuard(new TodayBranchClock());

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldThrow_WhenMemberIsRecordingOperatorButOlderDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var close = BuildClose(DailyCloseStatus.Draft,
            submittedByOperatorId: callerOperator.Id,
            date: DateTime.UtcNow.Date.AddDays(-1));
        var guard = BuildGuard(new TodayBranchClock());

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private static DailyCloseWorkflowGuard BuildGuard(IBranchClock clock)
        => new(clock);

    private static DailyClose BuildClose(
        DailyCloseStatus status,
        Guid? submittedByOperatorId = null,
        DateTime? date = null)
    {
        return new DailyCloseBuilder()
            .WithStatus(status)
            .WithSubmittedByOperator(submittedByOperatorId.HasValue
                ? new OperatorBuilder().WithId(submittedByOperatorId.Value).Build()
                : null)
            .WithDate(date ?? DateTime.UtcNow.Date)
            .Build();
    }

    /// <summary>
    /// Clock whose local business date always equals UTC today.
    /// The UTC instant is captured once at construction time so that all comparisons
    /// within a single test use the same moment — prevents flakiness near midnight.
    /// </summary>
    private sealed class TodayBranchClock : IBranchClock
    {
        private readonly DateTime _utcNow = DateTime.UtcNow;
        public DateTime UtcNow() => _utcNow;
        public DateTime LocalBusinessDate(DateTime utcInstant) => utcInstant.Date;
        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
            => localBusinessDate.Date == LocalBusinessDate(utcInstant);
    }
}
