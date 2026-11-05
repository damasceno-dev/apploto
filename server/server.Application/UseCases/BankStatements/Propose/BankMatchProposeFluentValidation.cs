using FluentValidation;
using server.Application.Services.Banking;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BankStatements.Propose;

public class BankMatchProposeFluentValidation : AbstractValidator<RequestBankMatchProposeJson>
{
    public BankMatchProposeFluentValidation() => RuleFor(r => r.ImportId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
}
