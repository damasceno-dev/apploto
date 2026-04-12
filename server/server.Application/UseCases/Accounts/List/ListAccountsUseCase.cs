using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Accounts.List;

public class ListAccountsUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository)
{
    public async Task<ResponseListAccountsJson> Execute()
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var accounts = await accountsRepository.ListActiveByBranchId(authenticatedBranchUser.BranchId);

        return accounts.ToResponse();
    }
}
