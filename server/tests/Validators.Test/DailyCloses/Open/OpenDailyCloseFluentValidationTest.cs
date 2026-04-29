using CommonTestUtilities.Requests;
using server.Application.UseCases.DailyCloses.Open;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.DailyCloses.Open;

public class OpenDailyCloseFluentValidationTest
{
    private readonly OpenDailyCloseFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequiredFieldsAreValid()
    {
        var request = new RequestOpenDailyCloseJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithDate(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_DATE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAccountIdIsEmpty()
    {
        var request = new RequestOpenDailyCloseJsonBuilder()
            .WithAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_REQUIRED);
    }
}
