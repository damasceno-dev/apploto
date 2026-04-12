using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.OperatorAccounts.AssignAccount;

public class AssignAccountFluentValidation : AbstractValidator<RequestAssignAccountJson>
{
    public AssignAccountFluentValidation()
    {
        RuleFor(r => r.AccountId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
    }
}
