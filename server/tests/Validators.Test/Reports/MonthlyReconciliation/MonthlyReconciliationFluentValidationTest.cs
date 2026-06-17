using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports.MonthlyReconciliation;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.MonthlyReconciliation;

public class MonthlyReconciliationFluentValidationTest
{
    private readonly MonthlyReconciliationFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenYearAndMonthAreValid()
    {
        var request = new RequestMonthlyReconciliationJsonBuilder()
            .WithYear(2025)
            .WithMonth(6)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void Validate_ShouldFail_WhenYearIsOutOfRange(int year)
    {
        var request = new RequestMonthlyReconciliationJsonBuilder()
            .WithYear(year)
            .WithMonth(6)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_ShouldFail_WhenMonthIsInvalid(int month)
    {
        var request = new RequestMonthlyReconciliationJsonBuilder()
            .WithYear(2025)
            .WithMonth(month)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_MONTH_INVALID);
    }
}
