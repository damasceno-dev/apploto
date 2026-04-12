using server.Communication.Responses;
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

        return account.ToAccountResponse();
    }
}
