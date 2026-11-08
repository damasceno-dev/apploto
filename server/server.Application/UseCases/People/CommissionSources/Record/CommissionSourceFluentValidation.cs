using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;
namespace server.Application.UseCases.People;

public class CommissionSourceFluentValidation : AbstractValidator<RequestCommissionSourceJson>
{
    public CommissionSourceFluentValidation()
    {
        RuleFor(r => r.PostedOn).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(500).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.Evidence).NotEmpty().MaximumLength(1000).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.PreviousId).NotEqual(Guid.Empty).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.OperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceDate).Must(PeopleValidation.Calendar).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SignedAmount).Must(v => PeopleValidation.Precision(v, 2)).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.ExternalIdentity).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceRole).Must(r => r is "BolaoLeftover" or "EnteredPrize").WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
        RuleFor(r => r.SourceRole).NotEmpty().MaximumLength(200).WithMessage(ResourcesErrorMessages.PEOPLE_INPUT_INVALID);
    }
}
