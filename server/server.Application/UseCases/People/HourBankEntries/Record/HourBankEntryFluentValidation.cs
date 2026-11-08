using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class HourBankEntryFluentValidation : AbstractValidator<RequestHourBankEntryJson>
{
    public HourBankEntryFluentValidation()
    {
        RuleFor(r => r.PostedOn).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PreviousId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.OperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Kind).IsInEnum().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceDate).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SignedHours).Must(v => PeopleValidation.Precision(v, 4)).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.ExternalIdentity).MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SignedHours).LessThan(0).When(r => r.Kind == server.Domain.Entities.Enums.HourBankEntryKind.Payment).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SignedHours).NotEqual(0).When(r => r.Kind != server.Domain.Entities.Enums.HourBankEntryKind.Opening).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
}
