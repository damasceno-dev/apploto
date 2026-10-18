using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.Logout;

public sealed class LogoutFluentValidation : AbstractValidator<RequestLogoutJson>
{
    public LogoutFluentValidation()
    {
        RuleFor(x => x.RefreshToken).NotEmpty().WithMessage(ResourcesErrorMessages.SECURITY_REFRESH_TOKEN_REQUIRED).MaximumLength(256).WithMessage(ResourcesErrorMessages.SECURITY_REFRESH_TOKEN_REQUIRED);
    }
}
