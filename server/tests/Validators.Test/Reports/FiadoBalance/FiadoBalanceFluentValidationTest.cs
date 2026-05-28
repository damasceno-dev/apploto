using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports.FiadoBalance;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.FiadoBalance;

public class FiadoBalanceFluentValidationTest
{
    private readonly FiadoBalanceFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenAllFieldsOmitted()
    {
        var request = new RequestFiadoBalanceJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAsOfDateIsValid()
    {
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(new DateTime(2025, 5, 28))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenClientIdIsProvided()
    {
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithClientId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAsOfDateIsDefault()
    {
        var request = new RequestFiadoBalanceJsonBuilder()
            .WithAsOfDate(default(DateTime))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
