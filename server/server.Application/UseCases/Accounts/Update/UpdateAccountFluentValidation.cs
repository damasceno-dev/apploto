using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.Update;

public class UpdateAccountFluentValidation : AbstractValidator<RequestUpdateAccountJson>
{
    internal const int NameMaxLength = 255;
    internal const int InstitutionMaxLength = 255;
    internal const int NumberMaxLength = 100;

    public UpdateAccountFluentValidation()
    {
        RuleFor(r => r.Name)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.NAME_EMPTY);

        RuleFor(r => r.Name)
            .MaximumLength(NameMaxLength)
            .When(r => string.IsNullOrWhiteSpace(r.Name) is false)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NAME_MAX_LENGTH, NameMaxLength));

        RuleFor(r => r.Institution)
            .MaximumLength(InstitutionMaxLength)
            .When(r => r.Institution is not null)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_INSTITUTION_MAX_LENGTH, InstitutionMaxLength));

        RuleFor(r => r.Number)
            .MaximumLength(NumberMaxLength)
            .When(r => r.Number is not null)
            .WithMessage(string.Format(ResourcesErrorMessages.ACCOUNT_NUMBER_MAX_LENGTH, NumberMaxLength));
    }
}
