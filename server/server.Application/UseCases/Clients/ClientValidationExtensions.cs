using System.Text.RegularExpressions;
using FluentValidation;
using server.Exceptions;

namespace server.Application.UseCases.Clients;

internal static class ClientValidationExtensions
{
    internal const int ClientNameMaxLength = 255;
    internal const int ClientPhoneMaxLength = 20;

    extension<T>(IRuleBuilder<T, string> rule)
    {
        public void ValidateClientName()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.NAME_EMPTY);
            rule
                .MaximumLength(ClientNameMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.CLIENT_NAME_MAX_LENGTH, ClientNameMaxLength));
        }

        public void ValidateClientPhone()
        {
            rule.NotEmpty().WithMessage(ResourcesErrorMessages.CLIENT_PHONE_EMPTY);
            rule
                .MaximumLength(ClientPhoneMaxLength)
                .WithMessage(string.Format(ResourcesErrorMessages.CLIENT_PHONE_MAX_LENGTH, ClientPhoneMaxLength));
        }
    }

    extension<T>(IRuleBuilder<T, string?> rule)
    {
        public IRuleBuilderOptions<T, string?> ValidClientCpf()
        {
            return rule
                .Must(cpf => Regex.Replace(cpf!, @"\D", "").Length == 11)
                .WithMessage(ResourcesErrorMessages.CLIENT_CPF_INVALID);
        }

        public IRuleBuilderOptions<T, string?> ValidClientEmail()
        {
            return rule
                .EmailAddress()
                .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        }
    }
}
