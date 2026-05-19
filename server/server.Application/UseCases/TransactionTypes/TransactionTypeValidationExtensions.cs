using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.TransactionTypes;

internal static class TransactionTypeValidationExtensions
{
    internal const int TransactionTypeNameMaxLength = 255;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateTransactionTypeName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
            rule
                .MaximumLength(TransactionTypeNameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_MAX_LENGTH, TransactionTypeNameMaxLength));
        }
    }
}
