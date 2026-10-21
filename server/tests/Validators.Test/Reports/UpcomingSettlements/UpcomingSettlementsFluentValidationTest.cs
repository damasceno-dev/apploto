using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.UpcomingSettlements;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.UpcomingSettlements;

public class UpcomingSettlementsFluentValidationTest
{
    private readonly UpcomingSettlementsFluentValidation _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData(SettlementInstrument.Cheque)]
    [InlineData(SettlementInstrument.CreditCard)]
    [InlineData(SettlementInstrument.DebitCard)]
    public void Validate_ShouldSucceed_WhenRequestIsValid(SettlementInstrument? instrument)
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithInstrument(instrument).Build();

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenWindowIsExactlyTheCap()
    {
        var from = new DateTime(2026, 1, 1);
        var request = new RequestUpcomingSettlementsJsonBuilder()
            .WithDueDateFrom(from)
            .WithDueDateTo(from.AddDays(ReportValidationExtensions.DateRangeMaxDays))
            .Build();

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenWindowExceedsTheCap()
    {
        var from = new DateTime(2026, 1, 1);
        var request = new RequestUpcomingSettlementsJsonBuilder()
            .WithDueDateFrom(from)
            .WithDueDateTo(from.AddDays(ReportValidationExtensions.DateRangeMaxDays + 1))
            .Build();

        ErrorsOf(request).ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_TOO_WIDE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDueDateFromIsMissing()
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithDueDateFrom(default).Build();

        // Only the missing date is reported: the window rules wait until both dates exist.
        ErrorsOf(request).ShouldBe([ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED]);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDueDateToIsMissing()
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithDueDateTo(default).Build();

        ErrorsOf(request).ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenWindowIsInverted()
    {
        var request = new RequestUpcomingSettlementsJsonBuilder()
            .WithDueDateFrom(new DateTime(2026, 11, 2))
            .WithDueDateTo(new DateTime(2026, 11, 1))
            .Build();

        ErrorsOf(request).ShouldContain(ResourcesErrorMessages.REPORT_DATE_RANGE_INVERTED);
    }

    [Theory]
    [InlineData(SettlementInstrument.None)]
    [InlineData((SettlementInstrument)99)]
    public void Validate_ShouldFail_WhenInstrumentIsNoneOrUndeclared(SettlementInstrument instrument)
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithInstrument(instrument).Build();

        ErrorsOf(request).ShouldBe([ResourcesErrorMessages.REPORT_SETTLEMENT_INSTRUMENT_INVALID]);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsBelowMinimum()
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithPage(ReportValidationExtensions.PageMin - 1).Build();

        ErrorsOf(request).ShouldContain(ResourcesErrorMessages.REPORT_PAGE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageSizeExceedsMaximum()
    {
        var request = new RequestUpcomingSettlementsJsonBuilder().WithPageSize(ReportValidationExtensions.PageSizeMax + 1).Build();

        ErrorsOf(request).ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }

    private List<string> ErrorsOf(server.Communication.Requests.RequestUpcomingSettlementsJson request)
    {
        var result = _validator.Validate(request);
        result.IsValid.ShouldBeFalse();
        return result.Errors.Select(error => error.ErrorMessage).ToList();
    }
}
