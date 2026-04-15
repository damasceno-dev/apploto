using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Accounts;

internal static class AccountValidationExtensions
{
    internal const int NameMaxLength = 255;
    internal const int InstitutionMaxLength = 255;
    internal const int NumberMaxLength = 100;

    public static void ValidateAccountName<T>(this IRuleBuilder<T, string> rule)
    {
        rule.NotEmpty()
            .WithMessage(ResourcesErrorMessages.NAME_EMPTY);

        rule.Must(name => string.IsNullOrWhiteSpace(name) || name.Length <= NameMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, NameMaxLength));
    }

    public static void ValidateOptionalAccountInstitution<T>(this IRuleBuilder<T, string?> rule)
    {
        rule.Must(institution => institution is null || institution.Length <= InstitutionMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, InstitutionMaxLength));
    }

    public static void ValidateOptionalAccountNumber<T>(this IRuleBuilder<T, string?> rule)
    {
        rule.Must(number => number is null || number.Length <= NumberMaxLength)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, NumberMaxLength));
    }
}
