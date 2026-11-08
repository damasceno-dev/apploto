using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class SpecialDrawAllocationFluentValidation : AbstractValidator<RequestSpecialDrawAllocationJson>
{
    public SpecialDrawAllocationFluentValidation()
    {
        RuleFor(r => r.PostedOn).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PreviousId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.DrawId).NotEmpty().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SessionId).NotEmpty().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SessionFieldEffectId).NotEmpty().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceDate).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Amount).Must(v => PeopleValidation.Precision(v, 2)).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Amount).GreaterThan(0).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceRevision).GreaterThan(0).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
}
