using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts.Create;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.Create;

public class CreateAccountFluentValidationTest
{
    private readonly CreateAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenTerminalWithNoTab()
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenTerminalWithTabAccountId()
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Terminal)
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenBankAccountWithNoTab()
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.BankAccount)
            .WithTabAccountId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenTabTypeWithNoTab()
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(AccountType.Tab)
            .WithTabAccountId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenOptionalFieldsAreProvided()
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithInstitution("Banco do Brasil")
            .WithNumber("12345-6")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateAccountJsonBuilder()
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
        var name = new string('a', CreateAccountFluentValidation.NameMaxLength + 1);

        var request = new RequestCreateAccountJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, CreateAccountFluentValidation.NameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstitutionExceedsMaxLength()
    {
        var institution = new string('a', CreateAccountFluentValidation.InstitutionMaxLength + 1);

        var request = new RequestCreateAccountJsonBuilder()
            .WithInstitution(institution)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, CreateAccountFluentValidation.InstitutionMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenNumberExceedsMaxLength()
    {
        var number = new string('a', CreateAccountFluentValidation.NumberMaxLength + 1);

        var request = new RequestCreateAccountJsonBuilder()
            .WithNumber(number)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, CreateAccountFluentValidation.NumberMaxLength));
    }

    [Theory]
    [InlineData(AccountType.BankAccount)]
    [InlineData(AccountType.Tab)]
    public void Validate_ShouldFail_WhenNonTerminalSetsTabAccountId(AccountType type)
    {
        var request = new RequestCreateAccountJsonBuilder()
            .WithType(type)
            .WithTabAccountId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ID_ONLY_FOR_TERMINAL);
    }
}
