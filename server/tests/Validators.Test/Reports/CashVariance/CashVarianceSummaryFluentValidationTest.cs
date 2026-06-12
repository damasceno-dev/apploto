using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.CashVariance;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.CashVariance;

public class CashVarianceSummaryFluentValidationTest
{
    private readonly CashVarianceSummaryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenAllRequiredFieldsAreValid()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAccountIdIsProvided()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithAccountId(Guid.NewGuid())
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsDefault()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(default)
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateToIsDefault()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(default)
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 2, 1))
            .WithDateTo(new DateTime(2025, 1, 1))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateRangeExceedsMaxDays()
    {
        var dateFrom = new DateTime(2025, 1, 1);
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(dateFrom)
            .WithDateTo(dateFrom.AddDays(ReportValidationExtensions.DateRangeMaxDays + 1))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(0)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsZero()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(1)
            .WithPageSize(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMax()
    {
        var request = new RequestCashVarianceSummaryJsonBuilder()
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .WithPage(1)
            .WithPageSize(ReportValidationExtensions.PageSizeMax + 1)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }
}
