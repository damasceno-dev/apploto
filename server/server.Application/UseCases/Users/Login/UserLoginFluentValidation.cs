using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.Login;

public class UserLoginFluentValidation : AbstractValidator<RequestUserLoginJson>
{
    public UserLoginFluentValidation()
    {
        RuleFor(r=> r.Email).NotEmpty().WithMessage(ResourcesErrorMessages.EMAIL_EMPTY);
        RuleFor(r => r.Email)
            .EmailAddress()
            .When(r => string.IsNullOrEmpty(r.Email) is false)
            .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        RuleFor(r => r.Password).ValidatePassword();
    }
}