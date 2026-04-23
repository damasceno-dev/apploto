using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Transactions;

internal static class TransactionValidationExtensions
{
    internal const decimal Numeric14X2UpperBound = 1_000_000_000_000m;
    internal const int TransactionDescriptionMaxLength = 500;

    extension<T>(IRuleBuilder<T, decimal> rule)
    {
        public IRuleBuilderOptions<T, decimal> ValueIsPositive()
        {
            return rule
                .GreaterThan(0m)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_VALUE_MUST_BE_POSITIVE);
        }

        // ReSharper disable once InconsistentNaming
        public void ValuePrecisionWithin14x2()
        {
            rule
                .Must(value => decimal.Round(value, 2) == value && value < Numeric14X2UpperBound)
                .WithMessage(ResourcesErrorMessages.TRANSACTION_VALUE_PRECISION_14X2);
        }
    }

    extension<T>(IRuleBuilder<T, DateTime> rule)
    {
        public void DueDateIsRequired()
        {
            rule
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.TRANSACTION_DUE_DATE_EMPTY);
        }

        public IRuleBuilderOptions<T, DateTime> DueDateOnOrAfterDate(Func<T, DateTime> dateSelector)
        {
            return rule
                .Must((instance, dueDate) => dueDate >= dateSelector(instance))
                .WithMessage(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
        }
    }

    extension<T>(IRuleBuilder<T, string?> rule)
    {
        public void DescriptionMaxLength()
        {
            rule
                .MaximumLength(TransactionDescriptionMaxLength)
                .WithMessage(string.Format(
                    ResourcesErrorMessages.TRANSACTION_DESCRIPTION_MAX_LENGTH,
                    TransactionDescriptionMaxLength));
        }
    }

    extension<T>(IRuleBuilder<T, DateTime?> rule)
    {
        public void DueDateOnOrAfterDate(Func<T, DateTime> dateSelector)
        {
            rule
                .Must((instance, dueDate) => dueDate is null || dueDate.Value >= dateSelector(instance))
                .WithMessage(ResourcesErrorMessages.TRANSACTION_DUE_DATE_BEFORE_DATE);
        }

        public IRuleBuilderOptions<T, DateTime?> PaidAtOnOrAfterDate(Func<T, DateTime> dateSelector)
        {
            return rule
                .Must((instance, paidAt) => paidAt is null || paidAt.Value >= dateSelector(instance))
                .WithMessage(ResourcesErrorMessages.TRANSACTION_PAID_AT_BEFORE_DATE);
        }
    }
}
