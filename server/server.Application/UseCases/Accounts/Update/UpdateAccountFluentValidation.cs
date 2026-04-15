using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.Update;

public class UpdateAccountFluentValidation : AbstractValidator<RequestUpdateAccountJson>
{
    public UpdateAccountFluentValidation()
    {
        RuleFor(r => r.Name).ValidateAccountName();
        RuleFor(r => r.Institution).ValidateOptionalAccountInstitution();
        RuleFor(r => r.Number).ValidateOptionalAccountNumber();
    }
}
