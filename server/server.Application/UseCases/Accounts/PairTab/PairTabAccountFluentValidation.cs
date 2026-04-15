using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.PairTab;

public class PairTabAccountFluentValidation : AbstractValidator<RequestPairTabAccountJson>
{
    public PairTabAccountFluentValidation()
    {
        RuleFor(r => r.TerminalAccountId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY);

        RuleFor(r => r.TabAccountId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TAB_ID_EMPTY);
    }
}
