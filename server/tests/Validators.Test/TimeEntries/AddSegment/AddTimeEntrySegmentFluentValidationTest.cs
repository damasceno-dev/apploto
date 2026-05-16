using CommonTestUtilities.Requests;
using server.Application.UseCases.TimeEntries.AddSegment;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TimeEntries.AddSegment;

public class AddTimeEntrySegmentFluentValidationTest
{
    private readonly AddTimeEntrySegmentFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestAddTimeEntrySegmentJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenClockInIsDefault()
    {
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenClockOutIsNotAfterClockIn()
    {
        var clockIn = new DateTime(2026, 5, 8, 12, 0, 0);
        var request = new RequestAddTimeEntrySegmentJsonBuilder()
            .WithClockIn(clockIn)
            .WithClockOut(clockIn)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN);
    }
}
