using server.Application.UseCases.Settings.LockMonth;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Settings.LockMonth;

public sealed class LockSettingMonthFluentValidationTest
{
    private readonly LockSettingMonthFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenYearAndMonthAreValid()
    {
        _validator.Validate(new RequestLockSettingMonthJson { Year = 2025, Month = 5 })
            .IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(LockSettingMonthFluentValidation.MinimumYear - 1)]
    [InlineData(LockSettingMonthFluentValidation.MaximumYear + 1)]
    public void Validate_ShouldFailWithExactKey_WhenYearIsOutOfRange(int year)
    {
        var result = _validator.Validate(new RequestLockSettingMonthJson { Year = year, Month = 5 });

        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.SETTING_LOCK_MONTH_YEAR_OUT_OF_RANGE,
                LockSettingMonthFluentValidation.MinimumYear,
                LockSettingMonthFluentValidation.MaximumYear));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_ShouldFailWithExactKey_WhenMonthIsOutOfRange(int month)
    {
        var result = _validator.Validate(new RequestLockSettingMonthJson { Year = 2025, Month = month });

        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.SETTING_LOCK_MONTH_INVALID);
    }
}
