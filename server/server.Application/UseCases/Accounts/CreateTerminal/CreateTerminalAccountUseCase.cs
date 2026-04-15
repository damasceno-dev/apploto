using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.CreateTerminal;

public class CreateTerminalAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateAccountJson> Execute(RequestCreateTerminalAccountJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var terminalAccount = AccountSharedMapper.ToDomain(
            AccountType.Terminal,
            authenticatedBranchUser.BranchId,
            request.Name,
            request.Institution,
            request.Number);

        if (request.ExistingTabAccountId.HasValue)
        {
            var existingTabAccount = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
                request.ExistingTabAccountId.Value,
                authenticatedBranchUser.BranchId);

            if (existingTabAccount is null || existingTabAccount.Type != AccountType.Tab)
            {
                throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
            }

            var linkedTerminalAccountId = await accountsRepository.GetActiveTerminalIdByTabAccountId(
                request.ExistingTabAccountId.Value,
                authenticatedBranchUser.BranchId);

            if (linkedTerminalAccountId.HasValue)
            {
                throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
            }

            terminalAccount.TabAccountId = request.ExistingTabAccountId.Value;
        }
        else if (request.CreateTabAccount)
        {
            var newTabAccount = AccountSharedMapper.ToDomain(
                AccountType.Tab,
                authenticatedBranchUser.BranchId,
                terminalAccount.Name,
                terminalAccount.Institution,
                terminalAccount.Number);

            terminalAccount.TabAccountId = newTabAccount.Id;
            terminalAccount.TabAccount = newTabAccount;
            await accountsRepository.Add(newTabAccount);
        }

        await accountsRepository.Add(terminalAccount);
        await unitOfWork.Commit();

        return terminalAccount.ToCreateResponse();
    }

    private static void Validate(RequestCreateTerminalAccountJson request)
    {
        var result = new CreateTerminalAccountFluentValidation().Validate(request);

        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
