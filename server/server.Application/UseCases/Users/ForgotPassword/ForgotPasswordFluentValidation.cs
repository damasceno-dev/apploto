using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.ForgotPassword;

public sealed class ForgotPasswordFluentValidation : AbstractValidator<RequestForgotPasswordJson>
{
    public ForgotPasswordFluentValidation()
    {
        RuleFor(x => x.Email).ValidateUserEmail();
    }
}
