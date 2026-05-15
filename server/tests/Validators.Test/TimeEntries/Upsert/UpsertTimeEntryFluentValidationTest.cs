using CommonTestUtilities.Requests;
using server.Application.UseCases.TimeEntries.Upsert;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TimeEntries.Upsert;

public class UpsertTimeEntryFluentValidationTest
{
    private readonly UpsertTimeEntryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenMemberTapShapeIsValid()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder().BuildMemberOpenTap();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAdminSnapshotShapeIsValid()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder().Build();
        var request = new RequestUpsertTimeEntryJsonBuilder().BuildAdminSnapshot(segment);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldNotRejectActionAndSegmentsTogether()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder().Build();
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithAction(TimeEntryTapAction.Open)
            .WithSegments(segment)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenOperatorIdIsEmpty()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithOperatorId(Guid.Empty)
            .BuildMemberOpenTap();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_OPERATOR_ID_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithDate(default)
            .BuildMemberOpenTap();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_DATE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenStatusIsOutOfEnumRange()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithStatus((TimeEntryStatus)999)
            .BuildMemberOpenTap();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenActionIsOutOfEnumRange()
    {
        var request = new RequestUpsertTimeEntryJsonBuilder()
            .WithAction((TimeEntryTapAction)999)
            .WithSegments((List<server.Communication.Requests.RequestTimeEntrySegmentJson>?)null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSegmentClockInIsDefault()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(default)
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder().BuildAdminSnapshot(segment);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenSegmentClockOutIsNotAfterClockIn()
    {
        var segment = new RequestTimeEntrySegmentJsonBuilder()
            .WithClockIn(new DateTime(2026, 5, 8, 12, 0, 0))
            .WithClockOut(new DateTime(2026, 5, 8, 12, 0, 0))
            .Build();
        var request = new RequestUpsertTimeEntryJsonBuilder().BuildAdminSnapshot(segment);

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }
}
