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
        RuleFor(r => r.Email).ValidateUserEmail();
        RuleFor(r => r.Password).ValidatePassword();
    }
}
