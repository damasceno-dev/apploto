using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.Get;

public class GetAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository)
{
    public async Task<ResponseAccountJson> Execute(Guid accountId)
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        //If it's a tab account, we attach its terminal account in the response
        var terminalAccountId = account.Type == AccountType.Tab
            ? await accountsRepository.GetActiveTerminalIdByTabAccountId(account.Id, authenticatedBranchUser.BranchId)
            : null;

        return account.ToAccountResponse(terminalAccountId);
    }
}
