using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class SpecialDrawFluentValidation : AbstractValidator<RequestSpecialDrawJson>
{
    public SpecialDrawFluentValidation()
    {
        RuleFor(r => r.PostedOn).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PreviousId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Modality).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Contest).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PeriodFrom).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PeriodTo).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Prize).Must(v => PeopleValidation.Precision(v, 2)).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Prize).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PeriodTo).GreaterThanOrEqualTo(r => r.PeriodFrom).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
}
