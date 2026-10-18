using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.UpdateProfile;

public sealed class UpdateProfileFluentValidation : AbstractValidator<RequestUpdateProfileJson>
{
    public UpdateProfileFluentValidation()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage(ResourcesErrorMessages.PROFILE_NAME_INVALID).MaximumLength(255).WithMessage(ResourcesErrorMessages.PROFILE_NAME_INVALID);
    }
}
