using CommonTestUtilities.Requests;
using server.Application.UseCases.TransactionTypes;
using server.Application.UseCases.TransactionTypes.Update;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TransactionTypes.Update;

public class UpdateTransactionTypeFluentValidationTest
{
    private readonly UpdateTransactionTypeFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestUpdateTransactionTypeJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameExceedsMaxLength()
    {
        var name = new string('a', TransactionTypeValidationExtensions.TransactionTypeNameMaxLength + 1);
        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithName(name)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.TRANSACTION_TYPE_NAME_MAX_LENGTH,
                TransactionTypeValidationExtensions.TransactionTypeNameMaxLength));
    }

    [Fact]
    public void Validate_ShouldFail_WhenSettlementRuleIsInvalid()
    {
        var request = new RequestUpdateTransactionTypeJsonBuilder()
            .WithSettlementRule((SettlementRule)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_RULE_INVALID);
    }
}
