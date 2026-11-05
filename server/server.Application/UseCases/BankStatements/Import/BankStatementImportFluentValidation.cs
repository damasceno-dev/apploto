using FluentValidation;
using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BankStatements.Import;

public class BankStatementImportFluentValidation : AbstractValidator<RequestBankStatementImportJson>
{
    public BankStatementImportFluentValidation()
    {
        RuleFor(r => r.AccountBindingId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.BankIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.AccountIdentity).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength(100).WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
        RuleFor(r => r.ContentBase64).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID).MaximumLength((OfxStatementParser.MaxBytes + 2) / 3 * 4).WithMessage(ResourcesErrorMessages.OFX_INVALID);
    }
}
