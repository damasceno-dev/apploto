using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports.DailyLedger;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.DailyLedger;

public class DailyLedgerFluentValidationTest
{
    private readonly DailyLedgerFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenAllFieldsValid()
    {
        var request = new RequestDailyLedgerJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAccountIdIsEmpty()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_ACCOUNT_ID_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsDefault()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithDateFrom(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateToIsDefault()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithDateTo(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsAfterDateTo()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithDateFrom(new DateTime(2025, 2, 1))
            .WithDateTo(new DateTime(2025, 1, 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateRangeExceeds366Days()
    {
        var dateFrom = new DateTime(2025, 1, 1);
        var request = new RequestDailyLedgerJsonBuilder()
            .WithDateFrom(dateFrom)
            .WithDateTo(dateFrom.AddDays(367))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithPage(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeIsZero()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithPageSize(0)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceeds200()
    {
        var request = new RequestDailyLedgerJsonBuilder()
            .WithPageSize(201)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }
}
