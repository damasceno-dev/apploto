using CommonTestUtilities.Requests;
using server.Application.UseCases.OperatorAccounts.AssignAccount;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.OperatorAccounts.AssignAccount;

public class AssignAccountFluentValidationTest
{
    private readonly AssignAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenAccountIdIsProvided()
    {
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(Guid.NewGuid())
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenAccountIdIsEmpty()
    {
        var request = new RequestAssignAccountJsonBuilder()
            .WithAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
    }
}
