using server.Application.Services.Receivables;
using server.Application.UseCases.Cheques;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.BankStatements.AccountBinding;

public class CreateBankStatementAccountBindingUseCase(
    BankCommandRunner runner,
    IBankStatementsRepository statements,
    IAccountsRepository accounts,
    IUnitOfWork unitOfWork)
{
    public Task<ResponseBankStatementAccountBindingJson> Execute(RequestBankStatementAccountBindingJson request, string? key, CancellationToken ct = default)
    {
        ChequeCommandValidation.Validate(new BankStatementAccountBindingFluentValidation(), request);
        return runner.Run<RequestBankStatementAccountBindingJson, ResponseBankStatementAccountBindingJson>("POST /bank-statement/account-binding", request, key,
            (_, _) => Task.FromResult<IReadOnlyCollection<Guid>>([request.BankAccountId]),
            async (actor, token) =>
            {
                var bank = await accounts.GetActiveByIdAndBranchIdAsNoTracking(request.BankAccountId, actor.BranchId, token);
                if (bank is null || bank.Type != AccountType.BankAccount) throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_BANK_NOT_FOUND);
                await statements.LockAccountBindingIdentity(actor.BranchId, request.BankIdentity.Trim(), request.AccountIdentity.Trim(), token);
                if (await statements.AccountBindingIdentityExists(actor.BranchId, request.BankIdentity.Trim(), request.AccountIdentity.Trim(), token))
                    throw new ConflictException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
                if ((await statements.ListAccountBindingsAsNoTracking(actor.BranchId, bank.Id, token)).Count != 0) throw new ConflictException(ResourcesErrorMessages.BANKING_STATE_CONFLICT);
                var binding = new BankStatementAccountBinding
                {
                    BranchId = actor.BranchId, BankAccountId = bank.Id, BankIdentity = request.BankIdentity.Trim(),
                    AccountIdentity = request.AccountIdentity.Trim(), CreatedByUserId = actor.UserId
                };
                await statements.AddAccountBinding(binding, token);
                await unitOfWork.Commit(token);
                return (binding.Id, binding.ToResponse());
            }, ct);
    }
}
