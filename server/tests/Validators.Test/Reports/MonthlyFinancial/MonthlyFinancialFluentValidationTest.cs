using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.MonthlyFinancial;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.MonthlyFinancial;

public class MonthlyFinancialFluentValidationTest
{
    private readonly MonthlyFinancialFluentValidation _validator = new();

    [Theory]
    [InlineData(ReportValidationExtensions.YearMin, 1)]
    [InlineData(2026, 10)]
    [InlineData(ReportValidationExtensions.YearMax, 12)]
    public void Validate_ShouldSucceed_WhenYearAndMonthAreInRange(int year, int month)
    {
        _validator.Validate(new RequestMonthlyFinancialReportJson { Year = year, Month = month }).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(ReportValidationExtensions.YearMin - 1)]
    [InlineData(ReportValidationExtensions.YearMax + 1)]
    public void Validate_ShouldFail_WhenYearIsOutOfRange(int year)
    {
        var result = _validator.Validate(new RequestMonthlyFinancialReportJson { Year = year, Month = 5 });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.REPORT_YEAR_OUT_OF_RANGE]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Validate_ShouldFail_WhenMonthIsInvalid(int month)
    {
        var result = _validator.Validate(new RequestMonthlyFinancialReportJson { Year = 2026, Month = month });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.REPORT_MONTH_INVALID]);
    }
}
