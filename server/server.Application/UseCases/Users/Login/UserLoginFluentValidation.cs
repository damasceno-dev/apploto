using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;

namespace server.Application.UseCases.Users.Login;

public class UserLoginFluentValidation : AbstractValidator<RequestUserLoginJson>
{
    public UserLoginFluentValidation()
    {
        RuleFor(r => r.Email).ValidateUserEmail();
        RuleFor(r => r.Password).ValidatePassword();
    }
}
