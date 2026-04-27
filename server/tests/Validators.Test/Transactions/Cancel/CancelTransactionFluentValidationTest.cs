using CommonTestUtilities.Requests;
using server.Application.UseCases.Transactions.Cancel;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.Cancel;

public class CancelTransactionFluentValidationTest
{
    private readonly CancelTransactionFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestShapeIsValid()
    {
        var request = new RequestCancelTransactionJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ShouldFail_WhenCancellationReasonIsEmpty(string reason)
    {
        var request = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason(reason)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_CANCELLATION_REASON_EMPTY);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenCancellationReasonIsExactlyAtMaxLength()
    {
        var request = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason(new string('a', CancelTransactionFluentValidation.CancellationReasonMaxLength))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCancellationReasonExceedsMaxLength()
    {
        var request = new RequestCancelTransactionJsonBuilder()
            .WithCancellationReason(new string('a', CancelTransactionFluentValidation.CancellationReasonMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.TRANSACTION_CANCELLATION_REASON_MAX_LENGTH,
                CancelTransactionFluentValidation.CancellationReasonMaxLength));
    }
}
