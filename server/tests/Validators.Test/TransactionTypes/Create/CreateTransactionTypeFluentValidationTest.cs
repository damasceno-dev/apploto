using CommonTestUtilities.Requests;
using server.Application.UseCases.TransactionTypes;
using server.Application.UseCases.TransactionTypes.Create;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.TransactionTypes.Create;

public class CreateTransactionTypeFluentValidationTest
{
    private readonly CreateTransactionTypeFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenRequestIsValid()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenCategoryIdIsEmpty()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_CATEGORY_ID_EMPTY);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder()
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
        var request = new RequestCreateTransactionTypeJsonBuilder()
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
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithSettlementRule((SettlementRule)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_RULE_INVALID);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(SettlementInstrument.None)]
    [InlineData(SettlementInstrument.Cheque)]
    [InlineData(SettlementInstrument.CreditCard)]
    [InlineData(SettlementInstrument.DebitCard)]
    public void Validate_ShouldSucceed_WhenSettlementInstrumentIsOmittedOrDeclared(SettlementInstrument? instrument)
    {
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithSettlementInstrument(instrument)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenSettlementInstrumentIsUndeclared()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithSettlementInstrument((SettlementInstrument)99)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_SETTLEMENT_INSTRUMENT_INVALID);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(MonthlyFinancialRole.None)]
    [InlineData(MonthlyFinancialRole.BolaoServiceFee)]
    [InlineData(MonthlyFinancialRole.CommercialLeftover)]
    public void Validate_ShouldSucceed_WhenMonthlyFinancialRoleIsOmittedOrDeclared(MonthlyFinancialRole? role)
    {
        var request = new RequestCreateTransactionTypeJsonBuilder().WithMonthlyFinancialRole(role).Build();

        _validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenMonthlyFinancialRoleIsUndeclared()
    {
        var request = new RequestCreateTransactionTypeJsonBuilder().WithMonthlyFinancialRole((MonthlyFinancialRole)42).Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_MONTHLY_FINANCIAL_ROLE_INVALID);
    }
}
