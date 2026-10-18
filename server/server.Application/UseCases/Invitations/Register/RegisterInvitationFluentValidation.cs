using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Invitations.Register;

public sealed class RegisterInvitationFluentValidation : AbstractValidator<RequestRegisterInvitationJson>
{
    public RegisterInvitationFluentValidation()
    {
        RuleFor(x => x.Token).Cascade(CascadeMode.Stop).NotEmpty().WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY).Length(43).WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY).Matches("^[A-Za-z0-9_-]+$").WithMessage(ResourcesErrorMessages.SECURITY_TOKEN_EMPTY);
        RuleFor(x => x.Name).NotEmpty().WithMessage(ResourcesErrorMessages.PROFILE_NAME_INVALID).MaximumLength(255).WithMessage(ResourcesErrorMessages.PROFILE_NAME_INVALID);
        RuleFor(x => x.Password).ValidatePassword();
    }
}
