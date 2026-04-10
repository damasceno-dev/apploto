using CommonTestUtilities.Requests;
using server.Application.UseCases.Branches.CreateSession;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Branches.CreateSession;

public class CreateBranchSessionFluentValidationTest
{
    private readonly CreateBranchSessionFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateBranchSessionJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenBranchIdIsEmpty()
    {
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.BRANCH_ID_EMPTY);
    }
}
