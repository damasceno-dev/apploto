using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Accounts.CreateTerminal;

public class CreateTerminalAccountFluentValidation : AbstractValidator<RequestCreateTerminalAccountJson>
{
    public CreateTerminalAccountFluentValidation()
    {
        RuleFor(request => request.Name).ValidateAccountName();
        RuleFor(request => request.Institution).ValidateOptionalAccountInstitution();
        RuleFor(request => request.Number).ValidateOptionalAccountNumber();

        RuleFor(request => request)
            .Must(request => request.ExistingTabAccountId.HasValue is false || request.CreateTabAccount is false)
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TERMINAL_CREATE_CANNOT_USE_EXISTING_AND_NEW_TAB);

        RuleFor(request => request.ExistingTabAccountId)
            .Must(existingTabAccountId => existingTabAccountId is null || existingTabAccountId.Value != Guid.Empty)
            .WithMessage(ResourcesErrorMessages.ACCOUNT_TAB_ID_EMPTY);
    }
}
