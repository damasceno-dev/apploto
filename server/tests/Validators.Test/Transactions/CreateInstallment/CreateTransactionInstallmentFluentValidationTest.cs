using CommonTestUtilities.Requests;
using server.Application.Services;
using server.Application.UseCases.Transactions;
using server.Application.UseCases.Transactions.CreateInstallment;
using server.Communication.Requests;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Transactions.CreateInstallment;

public class CreateTransactionInstallmentFluentValidationTest
{
    private readonly CreateTransactionInstallmentFluentValidation _validator = new();

    [Fact]
    public void Validate_ShouldSucceed_WhenManualInstallmentsAreValid()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder().Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CreateTransactionInstallmentFluentValidation.MinimumInstallments)]
    [InlineData(CreateTransactionInstallmentFluentValidation.MaximumInstallments)]
    public void Validate_ShouldSucceed_WhenAutoInstallmentCountIsWithinInclusiveBounds(int installmentCount)
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithAutoGeneration(installmentCount, DateTime.Today.AddDays(30))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(CreateTransactionInstallmentFluentValidation.MinimumInstallments - 1)]
    [InlineData(CreateTransactionInstallmentFluentValidation.MaximumInstallments + 1)]
    public void Validate_ShouldFail_WhenAutoInstallmentCountIsOutsideInclusiveBounds(int installmentCount)
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithAutoGeneration(installmentCount, DateTime.Today.AddDays(30))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE, CreateTransactionInstallmentFluentValidation.MinimumInstallments, CreateTransactionInstallmentFluentValidation.MaximumInstallments));
    }

    [Theory]
    [InlineData(CreateTransactionInstallmentFluentValidation.MinimumInstallments - 1)]
    [InlineData(CreateTransactionInstallmentFluentValidation.MaximumInstallments + 1)]
    public void Validate_ShouldFail_WhenManualInstallmentCountIsOutsideInclusiveBounds(int installmentCount)
    {
        var installments = Enumerable.Range(1, installmentCount)
            .Select(index => new RequestCreateTransactionInstallmentItemJson
            {
                DueDate = DateTime.Today.AddDays(index),
                Value = 1m
            })
            .ToList();
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(installmentCount)
            .WithInstallments(installments)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_COUNT_OUT_OF_RANGE, CreateTransactionInstallmentFluentValidation.MinimumInstallments, CreateTransactionInstallmentFluentValidation.MaximumInstallments));
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenFirstDueDateEqualsTransactionDateAndIsFuture()
    {
        var date = DateTime.Today.AddDays(10);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(date)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = date,
                    Value = 150m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = date.AddDays(10),
                    Value = 150m
                }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenRecordedByOperatorIdIsNull()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithRecordedByOperatorId(null)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDateIsDefault()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
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
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(SharedValidators.Numeric14X2UpperBound)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_PRECISION_14X2);
    }

    [Fact]
    public void Validate_ShouldFail_WhenManualInstallmentValueIsZero()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(100m)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = DateTime.Today.AddDays(30),
                    Value = 100m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = DateTime.Today.AddDays(60),
                    Value = 0m
                }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenInstallmentValuesDoNotMatchTotal()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithValue(301m)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_TOTAL_MISMATCH);
    }

    [Fact]
    public void Validate_ShouldFail_WhenManualDueDateIsBeforeTransactionDate()
    {
        var date = DateTime.Today.AddDays(20);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(date)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = date.AddDays(-1),
                    Value = 150m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = date.AddDays(10),
                    Value = 150m
                }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenFirstDueDateIsNotFuture()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(DateTime.Today)
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = DateTime.Today,
                    Value = 150m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = DateTime.Today.AddDays(10),
                    Value = 150m
                }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_FIRST_DUE_DATE_MUST_BE_FUTURE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenDueDatesAreNotStrictlyIncreasing()
    {
        var dueDate = DateTime.Today.AddDays(30);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithInstallments(
            [
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = dueDate,
                    Value = 150m
                },
                new RequestCreateTransactionInstallmentItemJson
                {
                    DueDate = dueDate,
                    Value = 150m
                }
            ])
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_DUE_DATES_MUST_INCREASE);
    }

    [Fact]
    public void Validate_ShouldFail_WhenAutoGenerateHasManualInstallments()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithAutoGenerationEnabled()
            .WithInstallmentCount(3)
            .WithDueDate(DateTime.Today.AddDays(30))
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_AUTO_GENERATE_CANNOT_HAVE_ITEMS);
    }

    [Fact]
    public void Validate_ShouldFail_WhenRecordedByOperatorIdSuppliedIsEmptyGuid()
    {
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithRecordedByOperatorId(Guid.Empty)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_RECORDED_BY_OPERATOR_ID_EMPTY);
    }

    [Fact]
    public void Validate_ShouldSucceed_WhenDescriptionIsAtInstallmentMaxLength()
    {
        var description = new string('a', TransactionValidationExtensions.InstallmentEffectiveDescriptionMaxLength);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDescription(description)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ShouldFail_WhenDescriptionExceedsInstallmentMaxLength()
    {
        // Operator-supplied description must leave room for the "CH PRE (XX/YY) - " prefix
        // so the persisted row description still fits varchar(500).
        var description = new string('a', TransactionValidationExtensions.InstallmentEffectiveDescriptionMaxLength + 1);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDescription(description)
            .Build();

        var result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.Select(e => e.ErrorMessage)
            .ShouldContain(string.Format(
                ResourcesErrorMessages.TRANSACTION_DESCRIPTION_MAX_LENGTH,
                TransactionValidationExtensions.InstallmentEffectiveDescriptionMaxLength));
    }
}
