using CommonTestUtilities.Entities;
using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class DailyCloseDraftTransitionTest
{
    private static readonly DateTime Now = new(2026, 8, 12, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ApplyRecall_ShouldClearSubmissionApprovalRejectionAndRecheck_ButRetainEligibility()
    {
        var close = BuildPopulatedClose(DailyCloseStatus.Submitted, "old rejection");
        var eligibility = close.ItemsFirstRecordedAt;
        var userId = Guid.NewGuid();

        new DailyCloseDraftTransition().ApplyRecall(close, Now, userId);

        AssertCommonReleasedState(close, userId, eligibility);
        close.RejectionReason.ShouldBeNull();
        AssertRecheckCleared(close);
    }

    [Fact]
    public void ApplyReopen_ShouldClearSubmissionApprovalRejectionAndRecheck_ButRetainEligibility()
    {
        var close = BuildPopulatedClose(DailyCloseStatus.Approved, "old rejection");
        var eligibility = close.ItemsFirstRecordedAt;
        var userId = Guid.NewGuid();

        new DailyCloseDraftTransition().ApplyReopen(close, Now, userId);

        AssertCommonReleasedState(close, userId, eligibility);
        close.RejectionReason.ShouldBeNull();
        AssertRecheckCleared(close);
    }

    [Fact]
    public void ApplyRejectedCorrection_ShouldRetainReasonAndExistingRecheckLineage()
    {
        var close = BuildPopulatedClose(DailyCloseStatus.Rejected, "count these again");
        var sourceId = close.OpeningRecheckTriggeredByDailyCloseId;
        var sourceUserId = close.OpeningRecheckTriggeredByUserId;
        var recheckAt = close.OpeningRecheckRequiredAt;

        new DailyCloseDraftTransition().ApplyRejectedCorrection(close, Now, Guid.NewGuid());

        close.Status.ShouldBe(DailyCloseStatus.Draft);
        close.SubmittedAt.ShouldBeNull();
        close.SubmittedByUserId.ShouldBeNull();
        close.SubmittedByOperatorId.ShouldBeNull();
        close.ApprovedAt.ShouldBeNull();
        close.ApprovedByUserId.ShouldBeNull();
        close.RejectionReason.ShouldBe("count these again");
        close.OpeningRecheckRequiredAt.ShouldBe(recheckAt);
        close.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(sourceId);
        close.OpeningRecheckTriggeredByUserId.ShouldBe(sourceUserId);
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    [InlineData(DailyCloseStatus.Rejected)]
    public void ApplyOpeningRecheck_ShouldStampLineageAndRetainRejectedReason(DailyCloseStatus status)
    {
        var reason = status == DailyCloseStatus.Rejected ? "retain me" : null;
        var close = BuildPopulatedClose(status, reason);
        var source = new DailyCloseBuilder().Build();
        var userId = Guid.NewGuid();

        new DailyCloseDraftTransition().ApplyOpeningRecheck(close, source, Now, userId);

        close.Status.ShouldBe(DailyCloseStatus.Draft);
        close.SubmittedAt.ShouldBeNull();
        close.SubmittedByUserId.ShouldBeNull();
        close.SubmittedByOperatorId.ShouldBeNull();
        close.ApprovedAt.ShouldBeNull();
        close.ApprovedByUserId.ShouldBeNull();
        close.RejectionReason.ShouldBe(reason);
        close.OpeningRecheckRequiredAt.ShouldBe(Now);
        close.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(source.Id);
        close.OpeningRecheckTriggeredByUserId.ShouldBe(userId);
        close.UpdatedAt.ShouldBe(Now);
        close.UpdatedByUserId.ShouldBe(userId);
    }

    private static server.Domain.Entities.DailyClose BuildPopulatedClose(
        DailyCloseStatus status,
        string? reason)
    {
        var source = new DailyCloseBuilder().Build();
        var recordingUser = new UserBuilder().Build();
        var recordingOperator = new OperatorBuilder().WithUser(recordingUser).Build();
        var submittingUser = new UserBuilder().Build();
        var submittingOperator = new OperatorBuilder().WithUser(submittingUser).Build();
        return new DailyCloseBuilder()
            .WithStatus(status)
            .WithRecordedBy(recordingUser, recordingOperator)
            .WithSubmittedBy(submittingUser, submittingOperator)
            .WithSubmittedAt(Now.AddHours(-2))
            .WithApprovedAt(Now.AddHours(-1))
            .WithApprovedByUser(new UserBuilder().Build())
            .WithRejectionReason(reason)
            .WithItemsFirstRecordedAt(Now.AddDays(-2))
            .WithOpeningRecheck(Now.AddDays(-1), source, Guid.NewGuid())
            .Build();
    }

    private static void AssertCommonReleasedState(
        server.Domain.Entities.DailyClose close,
        Guid userId,
        DateTime? eligibility)
    {
        close.Status.ShouldBe(DailyCloseStatus.Draft);
        close.SubmittedAt.ShouldBeNull();
        close.SubmittedByUserId.ShouldBeNull();
        close.SubmittedByOperatorId.ShouldBeNull();
        close.ApprovedAt.ShouldBeNull();
        close.ApprovedByUserId.ShouldBeNull();
        close.ItemsFirstRecordedAt.ShouldBe(eligibility);
        close.RecordedByUserId.ShouldNotBeNull();
        close.RecordedByOperatorId.ShouldNotBeNull();
        close.UpdatedAt.ShouldBe(Now);
        close.UpdatedByUserId.ShouldBe(userId);
    }

    private static void AssertRecheckCleared(server.Domain.Entities.DailyClose close)
    {
        close.OpeningRecheckRequiredAt.ShouldBeNull();
        close.OpeningRecheckTriggeredByDailyCloseId.ShouldBeNull();
        close.OpeningRecheckTriggeredByUserId.ShouldBeNull();
    }
}
