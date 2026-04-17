using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Operators;

internal static class OperatorValidationExtensions
{
    internal const int OperatorNameMaxLength = 255;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateOperatorName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.NAME_EMPTY);
            rule
                .MaximumLength(OperatorNameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.OPERATOR_NAME_MAX_LENGTH, OperatorNameMaxLength));
        }
    }

    extension<T>(IRuleBuilder<T, Guid?> rule)
    {
        public void ValidateOptionalUserId()
        {
            rule
                .NotEqual(Guid.Empty)
                .WithMessage(ResourcesErrorMessages.USER_ID_EMPTY);
        }
    }
}
