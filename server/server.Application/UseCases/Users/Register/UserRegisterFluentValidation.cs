using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.Register;

public class UserRegisterFluentValidation : AbstractValidator<RequestUserRegisterJson>
{
    public UserRegisterFluentValidation()
    {
        RuleFor(r => r.Name).NotEmpty().WithMessage(ResourcesErrorMessages.NAME_EMPTY);
        RuleFor(r=> r.Email).NotEmpty().WithMessage(ResourcesErrorMessages.EMAIL_EMPTY);
        RuleFor(r => r.Email)
            .EmailAddress()
            .When(r => string.IsNullOrEmpty(r.Email) is false)
            .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        RuleFor(r => r.Password).ValidatePassword();
    }
}
