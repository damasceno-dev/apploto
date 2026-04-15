using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts;
using server.Application.UseCases.Accounts.CreateBank;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.CreateBank;

public class CreateBankAccountFluentValidationTest
{
    private readonly CreateBankAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateBankAccountJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateBankAccountJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var request = new RequestCreateBankAccountJsonBuilder()
            .WithName(new string('a', AccountValidationExtensions.NameMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, AccountValidationExtensions.NameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstitutionExceedsMaxLength()
    {
        var request = new RequestCreateBankAccountJsonBuilder()
            .WithInstitution(new string('a', AccountValidationExtensions.InstitutionMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, AccountValidationExtensions.InstitutionMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNumberExceedsMaxLength()
    {
        var request = new RequestCreateBankAccountJsonBuilder()
            .WithNumber(new string('a', AccountValidationExtensions.NumberMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, AccountValidationExtensions.NumberMaxLength));
    }
}
