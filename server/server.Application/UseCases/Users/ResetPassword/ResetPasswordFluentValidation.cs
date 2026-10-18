using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.ResetPassword;

public sealed class ResetPasswordFluentValidation : AbstractValidator<RequestResetPasswordJson>
{
    public ResetPasswordFluentValidation()
    {
        RuleFor(x => x.Token).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY).Length(43).WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY).Matches("^[A-Za-z0-9_-]+$").WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY);
        RuleFor(x => x.NewPassword).ValidatePassword();
    }
}
