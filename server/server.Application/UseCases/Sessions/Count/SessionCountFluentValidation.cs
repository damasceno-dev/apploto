using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Sessions.Count;

public class ConferSessionCountFluentValidation : AbstractValidator<RequestConferSessionCountJson>
{
    public ConferSessionCountFluentValidation()
    {
        RuleFor(request => request.ConfirmationToken)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_REQUIRED);
        RuleFor(request => request.CountRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
        RuleFor(request => request.LedgerRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
        RuleFor(request => request.OpeningRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
    }
}

public class BeginSessionRecountFluentValidation : AbstractValidator<RequestBeginSessionRecountJson>
{
    public BeginSessionRecountFluentValidation()
    {
        RuleFor(request => request.PreviousAttemptId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.SESSION_CONFIRMATION_ATTEMPT_REQUIRED);
    }
}

public class SubmitSessionFluentValidation : AbstractValidator<RequestSubmitSessionJson>
{
    public SubmitSessionFluentValidation()
    {
        // An absent or stale AttemptId is a state conflict (409 SESSION_CONFIRMATION_NOT_CURRENT), decided after the
        // session's own submit preconditions, not an input-shape error.
        RuleFor(request => request.CountRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
        RuleFor(request => request.LedgerRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
        RuleFor(request => request.OpeningRevision).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.SESSION_REVISION_INVALID);
    }
}
