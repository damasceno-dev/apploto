using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.OperatorSummary;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.OperatorSummary;

public class OperatorTransactionSummaryFluentValidationTest
{
    private readonly OperatorTransactionSummaryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenOperatorIdIsProvided()
    {
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenMineIsTrueWithoutOperatorId()
    {
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateFromIsDefault()
    {
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(default)
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateToIsDefault()
    {
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(new DateTime(2025, 1, 1))
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
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(new DateTime(2025, 2, 1))
            .WithDateTo(new DateTime(2025, 1, 1))
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
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(dateFrom)
            .WithDateTo(dateFrom.AddDays(ReportValidationExtensions.DateRangeMaxDays + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenMineIsTrueAndOperatorIdIsProvided()
    {
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(true)
            .WithOperatorId(Guid.NewGuid())
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_MINE_AND_OPERATOR_ID_CONFLICT);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenMineIsFalseAndOperatorIdIsMissing()
    {
        // Valid input shape: a linked Member omitting both falls back to their own
        // operator. The Manager/Admin-only requirement is enforced in the use case.
        var request = new RequestOperatorTransactionSummaryJsonBuilder()
            .WithMine(false)
            .WithOperatorId(null)
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
