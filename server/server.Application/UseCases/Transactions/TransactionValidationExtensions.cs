using FluentValidation;
using server.Application.Services;
using server.Exceptions;

namespace server.Application.UseCases.Transactions;

internal static class TransactionValidationExtensions
{
    internal const int TransactionDescriptionMaxLength = 500;

    // Reserved budget for the auto-prefixed installment row description "CH PRE (XX/YY) - "
    // ("CH PRE (" = 8) + ("XX/YY" up to 5 with installment count <= 24) + (") - " = 4) = 17.
    internal const int InstallmentDescriptionPrefixReserve = 17;
    internal const int InstallmentEffectiveDescriptionMaxLength =
        TransactionDescriptionMaxLength - InstallmentDescriptionPrefixReserve;

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
            rule.ValuePrecisionWithin14x2(ResourcesErrorMessages.TRANSACTION_VALUE_PRECISION_14X2);
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

        public void InstallmentDescriptionMaxLength()
        {
            rule
                .MaximumLength(InstallmentEffectiveDescriptionMaxLength)
                .WithMessage(string.Format(
                    ResourcesErrorMessages.TRANSACTION_DESCRIPTION_MAX_LENGTH,
                    InstallmentEffectiveDescriptionMaxLength));
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
