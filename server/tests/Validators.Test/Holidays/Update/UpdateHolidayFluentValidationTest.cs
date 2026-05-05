using CommonTestUtilities.Requests;
using server.Application.UseCases.Holidays.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Holidays.Update;

public class UpdateHolidayFluentValidationTest
{
    private readonly UpdateHolidayFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDescriptionIsNull()
    {
        var request = new RequestUpdateHolidayJsonBuilder().WithDescription(null).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDescriptionIsWithinMaxLength()
    {
        var request = new RequestUpdateHolidayJsonBuilder()
            .WithDescription(new string('A', UpdateHolidayFluentValidation.MaximumDescriptionLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
    {
        var request = new RequestUpdateHolidayJsonBuilder()
            .WithDescription(new string('A', UpdateHolidayFluentValidation.MaximumDescriptionLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH,
                UpdateHolidayFluentValidation.MaximumDescriptionLength));
    }
}
