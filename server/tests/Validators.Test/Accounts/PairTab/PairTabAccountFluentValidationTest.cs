using CommonTestUtilities.Requests;
using server.Application.UseCases.Accounts.PairTab;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Accounts.PairTab;

public class PairTabAccountFluentValidationTest
{
    private readonly PairTabAccountFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestPairTabAccountJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenTerminalAccountIdIsEmpty()
    {
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTerminalAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTabAccountIdIsEmpty()
    {
        var request = new RequestPairTabAccountJsonBuilder()
            .WithTabAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.ACCOUNT_TAB_ID_EMPTY);
    }
}
