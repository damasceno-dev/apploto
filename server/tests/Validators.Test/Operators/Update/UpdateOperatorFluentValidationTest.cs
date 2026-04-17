using CommonTestUtilities.Requests;
using server.Application.UseCases.Operators;
using server.Application.UseCases.Operators.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Operators.Update;

public class UpdateOperatorFluentValidationTest
{
    private readonly UpdateOperatorFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateOperatorJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUserIdIsProvided()
    {
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenUserIdIsNull()
    {
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUpdateOperatorJsonBuilder()
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
        var name = new string('a', OperatorValidationExtensions.OperatorNameMaxLength + 1);

        var request = new RequestUpdateOperatorJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.OPERATOR_NAME_MAX_LENGTH, OperatorValidationExtensions.OperatorNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserIdIsEmptyGuid()
    {
        var request = new RequestUpdateOperatorJsonBuilder()
            .WithUserId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.USER_ID_EMPTY);
    }
}
