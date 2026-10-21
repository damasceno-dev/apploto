using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.ManagementOverview;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.ManagementOverview;

public class ManagementOverviewFluentValidationTest
{
    private readonly ManagementOverviewFluentValidation _validator = new();

    [Theory]
    [InlineData(null, null)]
    [InlineData(2026, 10)]
    [InlineData(ReportValidationExtensions.YearMin, 1)]
    public void Validate_ShouldSucceed_WhenBothOrNeitherAreGivenInRange(int? year, int? month)
    {
        _validator.Validate(new RequestManagementOverviewJson { Year = year, Month = month }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(2026, null)]
    [InlineData(null, 9)]
    public void Validate_ShouldFail_WhenOnlyOneOfYearAndMonthIsGiven(int? year, int? month)
    {
        var result = _validator.Validate(new RequestManagementOverviewJson { Year = year, Month = month });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_MONTH_PAIR_REQUIRED]);
    }

    [Fact]
    public void Validate_ShouldFail_WhenYearIsOutOfRange()
    {
        var result = _validator.Validate(new RequestManagementOverviewJson { Year = ReportValidationExtensions.YearMin - 1, Month = 1 });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_ShouldFail_WhenMonthIsInvalid(int month)
    {
        var result = _validator.Validate(new RequestManagementOverviewJson { Year = 2026, Month = month });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.REPORT_MONTH_INVALID]);
    }
}
