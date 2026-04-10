using CommonTestUtilities.Requests;
using server.Application.UseCases.BranchUsers.UpdateRole;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.BranchUsers.UpdateRole;

public class UpdateBranchUserRoleFluentValidationTest
{
    private readonly UpdateBranchUserRoleFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateBranchUserRoleJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenRoleIsMissing()
    {
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole((Role)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ROLE_INVALID);
    }
}
