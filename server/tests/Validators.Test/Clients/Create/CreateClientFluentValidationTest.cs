using CommonTestUtilities.Requests;
using server.Application.UseCases.Clients;
using server.Application.UseCases.Clients.Create;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Clients.Create;

public class CreateClientFluentValidationTest
{
    private readonly CreateClientFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenOnlyRequiredFieldsProvided()
    {
        var request = new RequestCreateClientJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenAllFieldsProvided()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("123.456.789-01")
            .WithEmail("cliente@example.com")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCpfIsUnformatted11Digits()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("12345678901")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCpfIsNull()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenEmailIsNull()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithEmail(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateClientJsonBuilder()
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
        var name = new string('a', ClientValidationExtensions.ClientNameMaxLength + 1);
        var request = new RequestCreateClientJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.CLIENT_NAME_MAX_LENGTH, ClientValidationExtensions.ClientNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneIsEmpty()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithPhone(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.CLIENT_PHONE_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneExceedsMaxLength()
    {
        var phone = new string('9', ClientValidationExtensions.ClientPhoneMaxLength + 1);
        var request = new RequestCreateClientJsonBuilder()
            .WithPhone(phone)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.CLIENT_PHONE_MAX_LENGTH, ClientValidationExtensions.ClientPhoneMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenCpfDoesNotHave11Digits()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("1234567890")  // 10 digits after normalization — one short
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.CLIENT_CPF_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailIsInvalid()
    {
        var request = new RequestCreateClientJsonBuilder()
            .WithEmail("not-an-email")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.EMAIL_INVALID);
    }
}
