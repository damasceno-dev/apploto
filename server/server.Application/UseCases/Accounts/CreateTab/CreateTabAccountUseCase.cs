using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.CreateTab;

public class CreateTabAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateAccountJson> Execute(RequestCreateTabAccountJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var terminalAccount = await accountsRepository.GetActiveByIdAndBranchId(
            request.TerminalAccountId,
            authenticatedBranchUser.BranchId);

        if (terminalAccount is null || terminalAccount.Type != AccountType.Terminal)
        {
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TERMINAL_NOT_FOUND);
        }

        if (terminalAccount.TabAccountId.HasValue)
        {
            throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TERMINAL_ALREADY_LINKED);
        }

        var tabAccount = AccountSharedMapper.ToDomain(
            AccountType.Tab,
            authenticatedBranchUser.BranchId,
            terminalAccount.Name,
            terminalAccount.Institution,
            terminalAccount.Number);

        terminalAccount.TabAccountId = tabAccount.Id;
        await accountsRepository.Add(tabAccount);
        await unitOfWork.Commit();

        return tabAccount.ToCreateResponse(terminalAccount.Id);
    }

    private static void Validate(RequestCreateTabAccountJson request)
    {
        var result = new CreateTabAccountFluentValidation().Validate(request);

        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
