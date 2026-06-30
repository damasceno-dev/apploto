using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.TimeEntryBalance;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.TimeEntryBalance;

public class TimeEntryBalanceSummaryFluentValidationTest
{
    private readonly TimeEntryBalanceSummaryFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenOperatorIdIsProvided()
    {
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
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
        // Valid input shape: omitting both is allowed — a linked Member falls back to
        // their own operator and a Manager/Admin gets the branch-wide roll-up (Phase 11
        // Addendum). No role-dependent rule lives in the validator.
        var request = new RequestTimeEntryBalanceSummaryJsonBuilder()
            .WithMine(false)
            .WithOperatorId(null)
            .WithDateFrom(new DateTime(2025, 1, 1))
            .WithDateTo(new DateTime(2025, 1, 31))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
