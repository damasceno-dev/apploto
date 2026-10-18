using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.ChangePassword;

public sealed class ChangePasswordFluentValidation : AbstractValidator<RequestChangePasswordJson>
{
    public ChangePasswordFluentValidation()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage(ResourcesErrorMessages.SECURITY_CURRENT_PASSWORD_REQUIRED);
        RuleFor(x => x.NewPassword).ValidatePassword();
    }
}
