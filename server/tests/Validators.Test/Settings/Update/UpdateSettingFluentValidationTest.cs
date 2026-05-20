using CommonTestUtilities.Requests;
using server.Application.UseCases.Settings.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Settings.Update;

public class UpdateSettingFluentValidationTest
{
    private readonly UpdateSettingFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsEmpty()
    {
        var request = new RequestUpdateSettingJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAllFieldsAreValid()
    {
        var request = new RequestUpdateSettingJsonBuilder()
            .WithDailyTargetHours(8.0m)
            .WithLunchDeductionOver6H(1.0m)
            .WithLunchDeductionOver4H(0.25m)
            .WithLockDate(new DateTime(2026, 6, 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDailyTargetHoursIsMax()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(24m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenLunchDeductionIsZero()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(0m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // ---- DailyTargetHours failures ----

    [Fact]
    public void Validate_ShouldFail_WhenDailyTargetHoursIsZero()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(0m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDailyTargetHoursExceeds24()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(24.01m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDailyTargetHoursHasMoreThanTwoDecimalPlaces()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithDailyTargetHours(7.333m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
    }

    // ---- LunchDeductionOver6H failures ----

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver6HIsNegative()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(-0.01m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver6HExceeds8()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(8.01m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver6HHasMoreThanTwoDecimalPlaces()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver6H(1.001m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    // ---- LunchDeductionOver4H failures ----

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver4HIsNegative()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver4H(-0.01m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver4HExceeds8()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver4H(8.01m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLunchDeductionOver4HHasMoreThanTwoDecimalPlaces()
    {
        var request = new RequestUpdateSettingJsonBuilder().WithLunchDeductionOver4H(0.001m).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorMessage == ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
    }
}
