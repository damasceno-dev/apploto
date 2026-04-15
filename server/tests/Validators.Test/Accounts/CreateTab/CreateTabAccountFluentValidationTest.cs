using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts.CreateTab;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.CreateTab;

public class CreateTabAccountFluentValidationTest
{
    private readonly CreateTabAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateTabAccountJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenTerminalAccountIdIsEmpty()
    {
        var request = new RequestCreateTabAccountJsonBuilder()
            .WithTerminalAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY);
    }
}
