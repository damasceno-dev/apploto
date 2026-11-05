using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BankStatements.AccountBinding;

public class BankStatementAccountBindingFluentValidation : AbstractValidator<RequestBankStatementAccountBindingJson>
{
    public BankStatementAccountBindingFluentValidation()
    {
        RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID)
            .Must(value => value is null || value.All(c => !char.IsControl(c))).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.AccountIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID)
            .Must(value => value is null || value.All(c => !char.IsControl(c))).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
    }
}
