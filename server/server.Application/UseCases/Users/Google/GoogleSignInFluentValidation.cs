using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Users.Google;

public sealed class GoogleSignInFluentValidation : AbstractValidator<RequestGoogleSignInJson>
{
    public GoogleSignInFluentValidation()
    {
        RuleFor(x => x.ChallengeId).NotEmpty().WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
        RuleFor(x => x.State).NotEmpty().WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID)
            .MaximumLength(128).WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
        RuleFor(x => x.IdToken).NotEmpty().WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID)
            .MaximumLength(16384).WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
        RuleFor(x => x.InvitationToken).MaximumLength(128).WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
        RuleFor(x => x.Name).MaximumLength(255).WithMessage(ResourcesErrorMessages.GOOGLE_ASSERTION_INVALID);
    }
}
