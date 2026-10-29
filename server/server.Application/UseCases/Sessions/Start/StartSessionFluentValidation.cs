using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Sessions.Start;

public class StartSessionFluentValidation : AbstractValidator<RequestStartSessionJson>
{
    public StartSessionFluentValidation()
    {
        RuleFor(request => request.AccountId).NotEmpty().WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        RuleFor(request => request.Opening).NotNull().WithMessage(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);
        RuleFor(request => request.Opening!).SetValidator(new SessionOpeningAcknowledgmentFluentValidation())
            .When(request => request.Opening is not null);
    }
}

public class SessionOpeningAcknowledgmentFluentValidation : AbstractValidator<RequestSessionOpeningAcknowledgmentJson>
{
    public SessionOpeningAcknowledgmentFluentValidation()
    {
        RuleFor(opening => opening.SourceKind).IsInEnum().WithMessage(ResourcesErrorMessages.SESSION_OPENING_SOURCE_KIND_INVALID);
        RuleFor(opening => opening.Items).OpeningItems();
        RuleFor(opening => opening.Reason).OptionalReason();
        RuleFor(opening => opening)
            .Must(opening => opening.HandoverId is null || opening.SourceKind is null)
            .WithMessage(ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_INVALID);
    }
}
