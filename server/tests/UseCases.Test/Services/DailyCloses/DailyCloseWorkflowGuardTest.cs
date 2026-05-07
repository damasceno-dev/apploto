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
    private static readonly DateTime StandardUtcNow = new(2026, 4, 25, 15, 0, 0, DateTimeKind.Utc);

    public enum CallerOperatorCase
    {
        NoLinkedOperator,
        RecordingOperator,
        OtherOperator
    }

    public enum BusinessDayCase
    {
        SameLocalBusinessDay,
        OlderLocalBusinessDay
    }

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin", Role.Admin }
    };

    public static TheoryData<string, DailyCloseStatus, Role, CallerOperatorCase, BusinessDayCase, DailyCloseEditItemsOutcome?>
        EnsureCanEditItemsCases => new()
        {
            {
                "Draft_MemberOwnOperatorSameDay_ReturnsEditOnDraft",
                DailyCloseStatus.Draft,
                Role.Member,
                CallerOperatorCase.RecordingOperator,
                BusinessDayCase.SameLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnDraft
            },
            {
                "Draft_MemberOtherOperator_Denied",
                DailyCloseStatus.Draft,
                Role.Member,
                CallerOperatorCase.OtherOperator,
                BusinessDayCase.SameLocalBusinessDay,
                null
            },
            {
                "Draft_Manager_ReturnsEditOnDraft",
                DailyCloseStatus.Draft,
                Role.Manager,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnDraft
            },
            {
                "Draft_Admin_ReturnsEditOnDraft",
                DailyCloseStatus.Draft,
                Role.Admin,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnDraft
            },
            {
                "Rejected_MemberOwnOperatorSameDay_ReturnsAutoTransitionToDraft",
                DailyCloseStatus.Rejected,
                Role.Member,
                CallerOperatorCase.RecordingOperator,
                BusinessDayCase.SameLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft
            },
            {
                "Rejected_Manager_ReturnsAutoTransitionToDraft",
                DailyCloseStatus.Rejected,
                Role.Manager,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft
            },
            {
                "Rejected_Admin_ReturnsAutoTransitionToDraft",
                DailyCloseStatus.Rejected,
                Role.Admin,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnRejectedAutoTransitionToDraft
            },
            {
                "Submitted_MemberOwnOperatorSameDay_ReturnsRecallToDraft",
                DailyCloseStatus.Submitted,
                Role.Member,
                CallerOperatorCase.RecordingOperator,
                BusinessDayCase.SameLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft
            },
            {
                "Submitted_MemberOtherOperator_Denied",
                DailyCloseStatus.Submitted,
                Role.Member,
                CallerOperatorCase.OtherOperator,
                BusinessDayCase.SameLocalBusinessDay,
                null
            },
            {
                "Submitted_MemberOwnOperatorOlderDay_Denied",
                DailyCloseStatus.Submitted,
                Role.Member,
                CallerOperatorCase.RecordingOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                null
            },
            {
                "Submitted_Manager_ReturnsRecallToDraft",
                DailyCloseStatus.Submitted,
                Role.Manager,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft
            },
            {
                "Submitted_Admin_ReturnsRecallToDraft",
                DailyCloseStatus.Submitted,
                Role.Admin,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.OlderLocalBusinessDay,
                DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft
            },
            {
                "Approved_Member_Denied",
                DailyCloseStatus.Approved,
                Role.Member,
                CallerOperatorCase.RecordingOperator,
                BusinessDayCase.SameLocalBusinessDay,
                null
            },
            {
                "Approved_Manager_Denied",
                DailyCloseStatus.Approved,
                Role.Manager,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.SameLocalBusinessDay,
                null
            },
            {
                "Approved_Admin_Denied",
                DailyCloseStatus.Approved,
                Role.Admin,
                CallerOperatorCase.NoLinkedOperator,
                BusinessDayCase.SameLocalBusinessDay,
                null
            }
        };

    public static TheoryData<string, DailyCloseStatus, Role> SubmitElevatedAllowedCases => new()
    {
        { "Draft_Manager", DailyCloseStatus.Draft, Role.Manager },
        { "Draft_Admin", DailyCloseStatus.Draft, Role.Admin },
        { "Rejected_Manager", DailyCloseStatus.Rejected, Role.Manager },
        { "Rejected_Admin", DailyCloseStatus.Rejected, Role.Admin }
    };

    public static TheoryData<string, DailyCloseStatus> SubmitMemberAllowedCases => new()
    {
        { "Draft_MemberOwnOperatorSameDay", DailyCloseStatus.Draft },
        { "Rejected_MemberOwnOperatorSameDay", DailyCloseStatus.Rejected }
    };

    public static TheoryData<string, DailyCloseStatus> NotSubmittableStatuses => new()
    {
        { "Submitted", DailyCloseStatus.Submitted },
        { "Approved", DailyCloseStatus.Approved }
    };

    public static TheoryData<string, DailyCloseStatus> NotReviewableStatuses => new()
    {
        { "Draft", DailyCloseStatus.Draft },
        { "Approved", DailyCloseStatus.Approved },
        { "Rejected", DailyCloseStatus.Rejected }
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public void EnsureCanOpen_ShouldAllowManagerAndAdmin_WhenCallerHasNoLinkedOperator(string _, Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanOpen(
            branchUser,
            callerOperator: null,
            Guid.NewGuid(),
            LocalToday()));
    }

    [Fact]
    public void EnsureCanOpen_ShouldAllowMember_WhenCallerHasLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanOpen(
            branchUser,
            callerOperator,
            Guid.NewGuid(),
            LocalToday()));
    }

    [Fact]
    public void EnsureCanOpen_ShouldRejectMember_WhenCallerHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() => guard.EnsureCanOpen(
            branchUser,
            callerOperator: null,
            Guid.NewGuid(),
            LocalToday()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Theory]
    [MemberData(nameof(EnsureCanEditItemsCases))]
    public void EnsureCanEditItems_ShouldFollowDocumentedMatrix(
        string _,
        DailyCloseStatus status,
        Role role,
        CallerOperatorCase callerOperatorCase,
        BusinessDayCase businessDayCase,
        DailyCloseEditItemsOutcome? expectedOutcome)
    {
        var submittedByOperatorId = Guid.NewGuid();
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var callerOperator = BuildCallerOperator(callerOperatorCase, branchUser, submittedByOperatorId);
        var clock = new FixedSaoPauloBranchClock(StandardUtcNow);
        var close = BuildClose(status, submittedByOperatorId, BusinessDate(clock, businessDayCase));
        var guard = BuildGuard(clock);

        if (expectedOutcome is { } outcome)
        {
            guard.EnsureCanEditItems(close, branchUser, callerOperator).ShouldBe(outcome);
            return;
        }

        var exception = Should.Throw<ConflictException>(() =>
            guard.EnsureCanEditItems(close, branchUser, callerOperator));
        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
    }

    [Fact]
    public void EnsureCanEditItems_ShouldUseSaoPauloLocalDay_WhenUtcDateCrossesMidnight()
    {
        var utcNowAfterMidnight = new DateTime(2026, 4, 26, 2, 30, 0, DateTimeKind.Utc);
        var clock = new FixedSaoPauloBranchClock(utcNowAfterMidnight);
        var localBusinessDate = new DateTime(2026, 4, 25);
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var close = BuildClose(
            DailyCloseStatus.Submitted,
            callerOperator.Id,
            localBusinessDate);
        var guard = BuildGuard(clock);

        var outcome = guard.EnsureCanEditItems(close, branchUser, callerOperator);

        outcome.ShouldBe(DailyCloseEditItemsOutcome.EditOnSubmittedRecallToDraft);
    }

    [Theory]
    [MemberData(nameof(SubmitElevatedAllowedCases))]
    public void EnsureCanSubmit_ShouldAllowManagerAndAdmin_WhenStatusIsDraftOrRejected(
        string _,
        DailyCloseStatus status,
        Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var close = BuildClose(status, Guid.NewGuid(), LocalToday().AddDays(-3));
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanSubmit(close, branchUser, callerOperator: null));
    }

    [Theory]
    [MemberData(nameof(SubmitMemberAllowedCases))]
    public void EnsureCanSubmit_ShouldAllowMember_WhenRecordingOperatorAndSameDay(string _, DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var close = BuildClose(status, callerOperator.Id, LocalToday());
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanSubmit(close, branchUser, callerOperator));
    }

    [Theory]
    [MemberData(nameof(NotSubmittableStatuses))]
    public void EnsureCanSubmit_ShouldThrowConflict_WhenStatusIsNotDraftOrRejected(
        string _,
        DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var close = BuildClose(status, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<ConflictException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator: null));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldRejectMember_WhenCallerHasNoLinkedOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var close = BuildClose(DailyCloseStatus.Draft, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator: null));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldRejectMember_WhenCallerIsNotRecordingOperator()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var close = BuildClose(DailyCloseStatus.Draft, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
    }

    [Fact]
    public void EnsureCanSubmit_ShouldRejectMember_WhenCloseIsOlderThanLocalBusinessDay()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var callerOperator = new OperatorBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithUserId(branchUser.UserId)
            .Build();
        var close = BuildClose(DailyCloseStatus.Draft, callerOperator.Id, LocalToday().AddDays(-1));
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanSubmit(close, branchUser, callerOperator));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
    }

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public void EnsureCanApprove_ShouldAllowManagerAndAdmin_WhenCloseIsSubmitted(string _, Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanApprove(close, branchUser));
    }

    [Fact]
    public void EnsureCanApprove_ShouldRejectMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanApprove(close, branchUser));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Theory]
    [MemberData(nameof(NotReviewableStatuses))]
    public void EnsureCanApprove_ShouldThrowConflict_WhenCloseIsNotSubmitted(
        string _,
        DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(status, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<ConflictException>(() => guard.EnsureCanApprove(close, branchUser));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE);
    }

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public void EnsureCanReject_ShouldAllowManagerAndAdmin_WhenCloseIsSubmitted(string _, Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        Should.NotThrow(() => guard.EnsureCanReject(close, branchUser));
    }

    [Fact]
    public void EnsureCanReject_ShouldRejectMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var close = BuildClose(DailyCloseStatus.Submitted, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<TokenWithoutPermissionException>(() =>
            guard.EnsureCanReject(close, branchUser));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Theory]
    [MemberData(nameof(NotReviewableStatuses))]
    public void EnsureCanReject_ShouldThrowConflict_WhenCloseIsNotSubmitted(
        string _,
        DailyCloseStatus status)
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var close = BuildClose(status, Guid.NewGuid(), LocalToday());
        var guard = BuildGuard();

        var exception = Should.Throw<ConflictException>(() => guard.EnsureCanReject(close, branchUser));

        exception.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_NOT_REJECTABLE);
    }

    private static DailyCloseWorkflowGuard BuildGuard()
        => BuildGuard(new FixedSaoPauloBranchClock(StandardUtcNow));

    private static DailyCloseWorkflowGuard BuildGuard(IBranchClock clock)
        => new(clock);

    private static DateTime LocalToday()
        => new FixedSaoPauloBranchClock(StandardUtcNow).LocalBusinessDate(StandardUtcNow);

    private static DateTime BusinessDate(FixedSaoPauloBranchClock clock, BusinessDayCase businessDayCase)
    {
        var today = clock.LocalBusinessDate(clock.UtcNow());
        return businessDayCase is BusinessDayCase.SameLocalBusinessDay
            ? today
            : today.AddDays(-1);
    }

    private static Operator? BuildCallerOperator(
        CallerOperatorCase callerOperatorCase,
        BranchUser branchUser,
        Guid submittedByOperatorId)
    {
        return callerOperatorCase switch
        {
            CallerOperatorCase.NoLinkedOperator => null,
            CallerOperatorCase.RecordingOperator => new OperatorBuilder()
                .WithId(submittedByOperatorId)
                .WithBranchId(branchUser.BranchId)
                .WithUserId(branchUser.UserId)
                .Build(),
            CallerOperatorCase.OtherOperator => new OperatorBuilder()
                .WithBranchId(branchUser.BranchId)
                .WithUserId(branchUser.UserId)
                .Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(callerOperatorCase), callerOperatorCase, null)
        };
    }

    private static DailyClose BuildClose(
        DailyCloseStatus status,
        Guid submittedByOperatorId,
        DateTime date)
    {
        return new DailyCloseBuilder()
            .WithStatus(status)
            .WithSubmittedByOperator(new OperatorBuilder().WithId(submittedByOperatorId).Build())
            .WithDate(date)
            .Build();
    }

    private sealed class FixedSaoPauloBranchClock(DateTime utcNow) : IBranchClock
    {
        private static readonly TimeZoneInfo BranchTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

        public DateTime UtcNow() => utcNow;

        public DateTime LocalBusinessDateTime(DateTime utcInstant)
        {
            var normalizedUtcInstant = utcInstant.Kind == DateTimeKind.Utc
                ? utcInstant
                : DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(normalizedUtcInstant, BranchTimeZone);
        }

        public DateTime LocalBusinessDate(DateTime utcInstant)
        {
            return LocalBusinessDateTime(utcInstant).Date;
        }

        public bool IsSameLocalDay(DateTime localBusinessDate, DateTime utcInstant)
        {
            return localBusinessDate.Date == LocalBusinessDate(utcInstant);
        }
    }
}
