using FluentValidation;
using server.Application.UseCases.BankStatements.AccountBinding;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;

namespace server.Application.UseCases.BankStatements.ListAccountBindings;

public class ListBankStatementAccountBindingsFluentValidation : AbstractValidator<RequestListBankStatementAccountBindingsJson>
{
    public ListBankStatementAccountBindingsFluentValidation() => RuleFor(r => r.BankAccountId).NotEmpty().WithMessage(ResourcesErrorMessages.BANKING_INPUT_INVALID);
}

public class ListBankStatementAccountBindingsUseCase(IAuthenticationService authentication, IBankStatementsRepository statements)
{
    public async Task<IReadOnlyList<ResponseBankStatementAccountBindingJson>> Execute(RequestListBankStatementAccountBindingsJson request, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new ListBankStatementAccountBindingsFluentValidation(), request);
        var actor = await authentication.GetAuthorizedBranchUser(Role.Manager, Role.Admin);
        return (await statements.ListAccountBindingsAsNoTracking(actor.BranchId, request.BankAccountId, ct)).Select(b => b.ToResponse()).ToList();
    }
}
