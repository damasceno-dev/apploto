using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.Deactivate;

public class DeactivateAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_ID_EMPTY]);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var account = await accountsRepository.GetActiveByIdAndBranchId(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var activeLinks = await operatorAccountsRepository.ListActiveByAccountId(account.Id);

        foreach (var link in activeLinks)
        {
            link.Active = false;
            link.IsPrimary = false;
        }

        account.Active = false;

        await unitOfWork.Commit();

        return account.ToAccountResponse();
    }
}
