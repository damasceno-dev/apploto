using server.Communication.Responses;
using server.Domain.Entities.Enums;
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
        var tabAccountIds = accounts
            .Where(account => account.Type == AccountType.Tab)
            .Select(account => account.Id)
            .ToArray();
        var terminalIdsByTabAccountId = tabAccountIds.Length == 0
            ? new Dictionary<Guid, Guid>()
            : await accountsRepository.ListActiveTerminalIdsByTabAccountIds(
                authenticatedBranchUser.BranchId,
                tabAccountIds);

        return accounts.ToResponse(terminalIdsByTabAccountId);
    }
}
