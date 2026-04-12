using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.Update;

public class UpdateAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(Guid accountId, RequestUpdateAccountJson request)
    {
        Validate(accountId, request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var account = await accountsRepository.GetActiveByIdAndBranchId(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        if (request.TabAccountId.HasValue && account.Type != AccountType.Terminal)
        {
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_TAB_ID_ONLY_FOR_TERMINAL]);
        }

        if (request.TabAccountId.HasValue)
        {
            await ValidateTabAccount(request.TabAccountId.Value, authenticatedBranchUser.BranchId, excludeAccountId: account.Id);
        }

        account.Name = request.Name.Trim();
        account.Institution = request.Institution?.Trim();
        account.Number = request.Number?.Trim();
        account.TabAccountId = request.TabAccountId;

        await unitOfWork.Commit();

        return account.ToAccountResponse();
    }

    private async Task ValidateTabAccount(Guid tabAccountId, Guid branchId, Guid excludeAccountId)
    {
        var tabAccount = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(tabAccountId, branchId);

        if (tabAccount is null || tabAccount.Type != AccountType.Tab)
        {
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        }

        var alreadyLinked = await accountsRepository.ExistsActiveTerminalForTabAccount(tabAccountId, excludeAccountId);

        if (alreadyLinked)
        {
            throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        }
    }

    private static void Validate(Guid accountId, RequestUpdateAccountJson request)
    {
        if (accountId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_ID_EMPTY]);
        }

        var result = new UpdateAccountFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
