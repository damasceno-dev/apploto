using CommonTestUtilities.Requests;
using server.Application.UseCases.Users.Register;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Users.Register;

public class UserRegisterFluentValidationTest
{
    private readonly UserRegisterFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUserRegisterJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUserRegisterJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage).ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailIsInvalid()
    {
        var request = new RequestUserRegisterJsonBuilder()
            .WithEmail("invalid-email")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage).ShouldContain(ResourcesErrorMessages.EMAIL_INVALID);
    }
}
