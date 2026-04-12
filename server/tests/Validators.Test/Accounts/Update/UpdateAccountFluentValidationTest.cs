using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.Update;

public class UpdateAccountFluentValidationTest
{
    private readonly UpdateAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateAccountJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenOptionalFieldsAreProvided()
    {
        var request = new RequestUpdateAccountJsonBuilder()
            .WithInstitution("Caixa Econômica")
            .WithNumber("9876-5")
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenOptionalFieldsAreNull()
    {
        var request = new RequestUpdateAccountJsonBuilder()
            .WithInstitution(null)
            .WithNumber(null)
            .WithTabAccountId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUpdateAccountJsonBuilder()
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
        var name = new string('a', UpdateAccountFluentValidation.NameMaxLength + 1);

        var request = new RequestUpdateAccountJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, UpdateAccountFluentValidation.NameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstitutionExceedsMaxLength()
    {
        var institution = new string('a', UpdateAccountFluentValidation.InstitutionMaxLength + 1);

        var request = new RequestUpdateAccountJsonBuilder()
            .WithInstitution(institution)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, UpdateAccountFluentValidation.InstitutionMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNumberExceedsMaxLength()
    {
        var number = new string('a', UpdateAccountFluentValidation.NumberMaxLength + 1);

        var request = new RequestUpdateAccountJsonBuilder()
            .WithNumber(number)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, UpdateAccountFluentValidation.NumberMaxLength));
    }
}
