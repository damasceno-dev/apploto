using FluentValidation;
using server.Exceptions;

namespace server.Application.Services;

public static class SharedValidators
{
    private const int MinimumPasswordLength = 8;
    public const int RefreshTokenExpirationTimeInDays = 7;

    public static void ValidatePassword<T>(this IRuleBuilder<T, string> passwordRule)
    {
        passwordRule.NotEmpty().WithMessage(ResourcesErrorMessages.PASSWORD_EMPTY);
        passwordRule.Must(p => string.IsNullOrWhiteSpace(p) ||p.Length >= MinimumPasswordLength).WithMessage(string.Format(ResourcesErrorMessages.PASSWORD_LENGTH, MinimumPasswordLength));
    }
}