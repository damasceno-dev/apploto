using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Accounts;

internal static class AccountValidationExtensions
{
    internal const int NameMaxLength = 255;
    internal const int InstitutionMaxLength = 255;
    internal const int NumberMaxLength = 100;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateAccountName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.NAME_EMPTY);
            rule
                .MaximumLength(NameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, NameMaxLength));
        }
    }

    extension<T>(IRuleBuilder<T, string?> rule)
    {
        public void ValidateOptionalAccountInstitution()
        {
            rule
                .MaximumLength(InstitutionMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, InstitutionMaxLength));
        }

        public void ValidateOptionalAccountNumber()
        {
            rule
                .MaximumLength(NumberMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, NumberMaxLength));
        }
    }
}
