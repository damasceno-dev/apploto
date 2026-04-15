using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.CreateTab;

public class CreateTabAccountFluentValidation : AbstractValidator<RequestCreateTabAccountJson>
{
    public CreateTabAccountFluentValidation()
    {
        RuleFor(request => request.TerminalAccountId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY);
    }
}
