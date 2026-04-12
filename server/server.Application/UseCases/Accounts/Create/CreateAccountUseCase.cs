using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.Create;

public class CreateAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateAccountJson> Execute(RequestCreateAccountJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (request.TabAccountId.HasValue)
        {
            await ValidateTabAccount(request.TabAccountId.Value, authenticatedBranchUser.BranchId, excludeAccountId: null);
        }

        var account = request.ToDomain(authenticatedBranchUser.BranchId);

        await accountsRepository.Add(account);
        await unitOfWork.Commit();

        return account.ToCreateResponse();
    }

    private async Task ValidateTabAccount(Guid tabAccountId, Guid branchId, Guid? excludeAccountId)
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

    private static void Validate(RequestCreateAccountJson request)
    {
        var result = new CreateAccountFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
