using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.UnpairTab;

public class UnpairTabAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(Guid terminalAccountId)
    {
        Validate(terminalAccountId);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var terminalAccount = await accountsRepository.GetActiveByIdAndBranchId(terminalAccountId, authenticatedBranchUser.BranchId);

        if (terminalAccount is null || terminalAccount.Type != AccountType.Terminal)
        {
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
        }

        if (terminalAccount.TabAccountId.HasValue is false)
        {
            return terminalAccount.ToAccountResponse();
        }

        terminalAccount.TabAccountId = null;
        await unitOfWork.Commit();

        return terminalAccount.ToAccountResponse();
    }

    private static void Validate(Guid terminalAccountId)
    {
        if (terminalAccountId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_TERMINAL_ID_EMPTY]);
        }
    }
}
