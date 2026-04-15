using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts;
using server.Application.UseCases.Accounts.CreateTerminal;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.CreateTerminal;

public class CreateTerminalAccountFluentValidationTest
{
    private readonly CreateTerminalAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenTerminalHasNoTab()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenTerminalUsesExistingTab()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenTerminalCreatesNewTab()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithCreateTabAccount(true)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenRequestUsesExistingAndNewTabTogether()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(Guid.NewGuid())
            .WithCreateTabAccount(true)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_CREATE_CANNOT_USE_EXISTING_AND_NEW_TAB);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder()
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
        var request = new RequestCreateTerminalAccountJsonBuilder()
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
        var request = new RequestCreateTerminalAccountJsonBuilder()
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
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithNumber(new string('a', AccountValidationExtensions.NumberMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, AccountValidationExtensions.NumberMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenExistingTabAccountIdIsEmpty()
    {
        var request = new RequestCreateTerminalAccountJsonBuilder()
            .WithExistingTabAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ID_EMPTY);
    }
}
