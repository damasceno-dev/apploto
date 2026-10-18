using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Users;

internal static class UserValidationExtensions
{
    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateUserEmail()
        {
            rule.MaximumLength(255).WithMessage(ResourcesErrorMessages.SECURITY_EMAIL_MAX_LENGTH);
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.EMAIL_EMPTY);
            rule
                .Must(email =>
                    string.IsNullOrEmpty(email) ||
                    new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
                .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        }
    }
}
