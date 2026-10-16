using CommonTestUtilities.Requests;
using server.Application.UseCases.Reports.ManagerQueue;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Reports.ManagerQueue;

public class ManagerQueueFluentValidationTest
{
    private readonly ManagerQueueFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenDateIsValid()
    {
        var request = new RequestManagerQueueJsonBuilder()
            .WithDate(new DateTime(2025, 5, 28))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestManagerQueueJsonBuilder()
            .WithDate(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.REPORT_AS_OF_DATE_INVALID);
    }
}
