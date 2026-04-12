using FluentValidation;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.Create;

public class CreateAccountFluentValidation : AbstractValidator<RequestCreateAccountJson>
{
    internal const int NameMaxLength = 255;
    internal const int InstitutionMaxLength = 255;
    internal const int NumberMaxLength = 100;

    public CreateAccountFluentValidation()
    {
        RuleFor(r => r.Type)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TYPE_INVALID);

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

        RuleFor(r => r.TabAccountId)
            .Must((request, tabAccountId) => tabAccountId is null || request.Type == AccountType.Terminal)
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TAB_ID_ONLY_FOR_TERMINAL);
    }
}
