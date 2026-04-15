using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.CreateBank;

public class CreateBankAccountFluentValidation : AbstractValidator<RequestCreateBankAccountJson>
{
    public CreateBankAccountFluentValidation()
    {
        RuleFor(request => request.Name).ValidateAccountName();
        RuleFor(request => request.Institution).ValidateOptionalAccountInstitution();
        RuleFor(request => request.Number).ValidateOptionalAccountNumber();
    }
}
