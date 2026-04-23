using CommonTestUtilities.Requests;
using server.Application.UseCases.Transactions;
using server.Application.UseCases.Transactions.Update;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.Update;

public class UpdateTransactionFluentValidationTest
{
    private readonly UpdateTransactionFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestShapeIsValid()
    {
        var request = new RequestUpdateTransactionJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDescriptionIsNull()
    {
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDueDateIsDefault()
    {
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDueDate(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsMaxLength()
    {
        var request = new RequestUpdateTransactionJsonBuilder()
            .WithDescription(new string('a', TransactionValidationExtensions.TransactionDescriptionMaxLength + 1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(error => error.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.TRANSACTION_DESCRIPTION_MAX_LENGTH, TransactionValidationExtensions.TransactionDescriptionMaxLength));
    }
}
