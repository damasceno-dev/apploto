using CommonTestUtilities.Requests;
using server.Application.UseCases.Transactions.Create;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.Create;

public class CreateTransactionFluentValidationTest
{
    private readonly CreateTransactionFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequiredFieldsValid()
    {
        var request = new RequestCreateTransactionJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDueDateEqualsDate()
    {
        var date = new DateTime(2025, 3, 1);
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(date)
            .WithDueDate(date)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenRecordedByOperatorIdIsNull()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithRecordedByOperatorId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(default)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DATE_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenTransactionTypeIdIsEmpty()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithTransactionTypeId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_ID_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAccountIdIsEmpty()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithAccountId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_ACCOUNT_ID_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenValueIsZero()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(0m)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenValueIsNegative()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(-1.50m)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenValueHasMoreThanTwoDecimalPlaces()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(10.123m)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_PRECISION_14X2);
    }

    [Fact]
    public void Validate_ShouldFail_WhenValueExceeds14x2UpperBound()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithValue(1_000_000_000_000m)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_PRECISION_14X2);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDueDateIsBeforeDate()
    {
        var date = new DateTime(2025, 3, 10);
        var request = new RequestCreateTransactionJsonBuilder()
            .WithDate(date)
            .WithDueDate(date.AddDays(-1))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRecordedByOperatorIdSuppliedIsEmptyGuid()
    {
        var request = new RequestCreateTransactionJsonBuilder()
            .WithRecordedByOperatorId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_RECORDED_BY_OPERATOR_ID_EMPTY);
    }
}
