using CommonTestUtilities.Requests;
using server.Application.UseCases.DailyCloses.Reject;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.DailyCloses.Reject;

public class RejectDailyCloseFluentValidationTest
{
    private readonly RejectDailyCloseFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestShapeIsValid()
    {
        var request = new RequestRejectDailyCloseJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenRejectionReasonIsEmpty(string reason)
    {
        var request = new RequestRejectDailyCloseJsonBuilder()
            .WithRejectionReason(reason)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_REJECTION_REASON_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenRejectionReasonIsExactlyAtMaxLength()
    {
        var request = new RequestRejectDailyCloseJsonBuilder()
            .WithRejectionReason(new string('a', RejectDailyCloseFluentValidation.RejectionReasonMaxLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenRejectionReasonExceedsMaxLength()
    {
        var request = new RequestRejectDailyCloseJsonBuilder()
            .WithRejectionReason(new string('a', RejectDailyCloseFluentValidation.RejectionReasonMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.DAILYCLOSE_REJECTION_REASON_LENGTH,
                RejectDailyCloseFluentValidation.RejectionReasonMaxLength));
    }
}
