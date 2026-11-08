using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class PayCategoryFluentValidation : AbstractValidator<RequestPayCategoryJson>
{
    public PayCategoryFluentValidation()
    {
        RuleFor(r => r.PostedOn).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PreviousId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Name).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Kind).IsInEnum().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
}
