using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Accounts.CreateSafe;

public class CreateSafeAccountFluentValidation : AbstractValidator<RequestCreateSafeAccountJson>
{
    public CreateSafeAccountFluentValidation()
    {
        RuleFor(request => request.Name).ValidateAccountName();
        RuleFor(request => request.Institution).ValidateOptionalAccountInstitution();
        RuleFor(request => request.Number).ValidateOptionalAccountNumber();
    }
}
