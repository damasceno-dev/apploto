using CommonTestUtilities.Requests;
using server.Application.UseCases.BranchUsers.Add;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.BranchUsers.Add;

public class AddBranchUserFluentValidationTest
{
    private readonly AddBranchUserFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestUsesUserId()
    {
        var request = new RequestAddBranchUserJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenUserReferenceIsMissing()
    {
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(null)
            .WithEmail(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.BRANCH_USER_IDENTIFIER_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenBothUserIdAndEmailAreProvided()
    {
        var request = new RequestAddBranchUserJsonBuilder()
            .WithEmail("member@example.com")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.BRANCH_USER_IDENTIFIER_EXCLUSIVE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailIsInvalid()
    {
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(null)
            .WithEmail("invalid-email")
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.EMAIL_INVALID);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRoleIsMissing()
    {
        var request = new RequestAddBranchUserJsonBuilder()
            .WithRole(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ROLE_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRoleIsOutsideEnumRange()
    {
        var request = new RequestAddBranchUserJsonBuilder()
            .WithRole((Role)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ROLE_INVALID);
    }
}
