using server.Application.UseCases.Settings.Update;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Settings.Update;

public class PersonalShortageThresholdFluentValidationTest
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("1000000000000")]
    public void InvalidThreshold_ShouldFailWithoutChangingOtherPolicy(string raw)
    {
        var value = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);
        new UpdateSettingFluentValidation().Validate(new RequestUpdateSettingJson { PersonalShortageThreshold = value })
            .Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.PERSONAL_SHORTAGE_THRESHOLD_INVALID);
    }

    [Fact]
    public void ThresholdOmissionExplicitValueAndClear_AreDifferentValidIntents()
    {
        var validator = new UpdateSettingFluentValidation();
        validator.Validate(new RequestUpdateSettingJson()).IsValid.ShouldBeTrue();
        validator.Validate(new RequestUpdateSettingJson { PersonalShortageThreshold = 0.01m }).IsValid.ShouldBeTrue();
        validator.Validate(new RequestUpdateSettingJson { ClearPersonalShortageThreshold = true }).IsValid.ShouldBeTrue();
        validator.Validate(new RequestUpdateSettingJson { PersonalShortageThreshold = 20m, ClearPersonalShortageThreshold = true }).IsValid.ShouldBeFalse();
    }
}
