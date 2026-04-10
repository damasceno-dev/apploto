using CommonTestUtilities.Requests;
using server.Application.UseCases.Branches.Create;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Branches.Create;

public class CreateBranchFluentValidationTest
{
    private readonly CreateBranchFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateBranchJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenOnlyNameIsProvided()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithCnpj(null)
            .WithAddress(null)
            .WithPhone(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenNameIsAtMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithName(new string('A', CreateBranchFluentValidation.BranchNameMaxLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithName(new string('A', CreateBranchFluentValidation.BranchNameMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.BRANCH_NAME_MAX_LENGTH, CreateBranchFluentValidation.BranchNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCnpjIsAtMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithCnpj(new string('1', CreateBranchFluentValidation.BranchCnpjMaxLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCnpjExceedsMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithCnpj(new string('1', CreateBranchFluentValidation.BranchCnpjMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.BRANCH_CNPJ_MAX_LENGTH, CreateBranchFluentValidation.BranchCnpjMaxLength));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenPhoneIsAtMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithPhone(new string('1', CreateBranchFluentValidation.BranchPhoneMaxLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneExceedsMaximumLength()
    {
        var request = new RequestCreateBranchJsonBuilder()
            .WithPhone(new string('1', CreateBranchFluentValidation.BranchPhoneMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.BRANCH_PHONE_MAX_LENGTH, CreateBranchFluentValidation.BranchPhoneMaxLength));
    }
}
