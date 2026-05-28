using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports;
using server.Application.UseCases.Reports.FiadoAging;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.FiadoAging;

public class FiadoAgingFluentValidationTest
{
    private readonly FiadoAgingFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenAllOptionalFieldsOmittedAndPagingValid()
    {
        var request = new RequestFiadoAgingJsonBuilder()
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAsOfDateIsValid()
    {
        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(new DateTime(2025, 5, 28))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenClientIdAndAccountIdAreProvided()
    {
        var request = new RequestFiadoAgingJsonBuilder()
            .WithClientId(Guid.NewGuid())
            .WithAccountId(Guid.NewGuid())
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAsOfDateIsDefault()
    {
        var request = new RequestFiadoAgingJsonBuilder()
            .WithAsOfDate(default(DateTime))
            .WithPage(1)
            .WithPageSize(50)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPageIsZero()
    {
        var request = new RequestFiadoAgingJsonBuilder()
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
        var request = new RequestFiadoAgingJsonBuilder()
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
        var request = new RequestFiadoAgingJsonBuilder()
            .WithPage(1)
            .WithPageSize(ReportValidationExtensions.PageSizeMax + 1)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }
}
