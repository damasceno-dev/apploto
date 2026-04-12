using CommonTestUtilities.Requests;
using server.Application.UseCases.Operators.Create;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Operators.Create;

public class CreateOperatorFluentValidationTest
{
    private readonly CreateOperatorFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateOperatorJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUserIdIsProvided()
    {
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUserIdIsNull()
    {
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateOperatorJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var name = new string('a', CreateOperatorFluentValidation.OperatorNameMaxLength + 1);

        var request = new RequestCreateOperatorJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.OPERATOR_NAME_MAX_LENGTH, CreateOperatorFluentValidation.OperatorNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdIsEmptyGuid()
    {
        var request = new RequestCreateOperatorJsonBuilder()
            .WithUserId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.USER_ID_EMPTY);
    }
}
