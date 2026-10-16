using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.Deactivate;

public class DeactivateAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IAccountPairingCoordination accountPairingCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_ID_EMPTY]);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        await using var coordination = await accountPairingCoordination.TryAcquire(authenticatedBranchUser.BranchId)
            ?? throw new ConflictException(ResourcesErrorMessages.ACCOUNT_PAIRING_COORDINATION_BUSY);

        var account = await accountsRepository.GetActiveByIdAndBranchId(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var activeLinks = await operatorAccountsRepository.ListActiveByAccountId(account.Id);

        foreach (var link in activeLinks)
        {
            link.Active = false;
            link.IsPrimary = false;
        }

        // A deactivated account never keeps a pairing (decision 1.8, gap S): a Terminal releases
        // its Tab, and a Tab is released by every Terminal that holds it, in the same commit.
        if (account.Type == AccountType.Terminal)
        {
            account.TabAccountId = null;
        }
        else if (account.Type == AccountType.Tab)
        {
            var pairedTerminals = await accountsRepository.ListTerminalsByTabAccountId(
                authenticatedBranchUser.BranchId,
                account.Id);

            foreach (var terminal in pairedTerminals)
                terminal.TabAccountId = null;
        }

        account.Active = false;

        await unitOfWork.Commit();
        await coordination.Complete();

        return account.ToAccountResponse();
    }
}
