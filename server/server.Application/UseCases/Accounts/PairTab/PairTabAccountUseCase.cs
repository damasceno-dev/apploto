using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.PairTab;

public class PairTabAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseAccountJson> Execute(RequestPairTabAccountJson request)
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

        var tabAccount = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
            request.TabAccountId,
            authenticatedBranchUser.BranchId);

        if (tabAccount is null || tabAccount.Type != AccountType.Tab)
        {
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_TAB_NOT_FOUND);
        }

        var linkedTerminalAccountId = await accountsRepository.GetActiveTerminalIdByTabAccountId(
            request.TabAccountId,
            authenticatedBranchUser.BranchId);

        if (linkedTerminalAccountId.HasValue && linkedTerminalAccountId.Value != terminalAccount.Id)
        {
            throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TAB_ALREADY_LINKED);
        }

        if (terminalAccount.TabAccountId == request.TabAccountId)
        {
            return terminalAccount.ToAccountResponse();
        }

        if (terminalAccount.TabAccountId.HasValue)
        {
            throw new ConflictException(ResourcesErrorMessages.ACCOUNT_TERMINAL_ALREADY_LINKED);
        }

        terminalAccount.TabAccountId = request.TabAccountId;
        await unitOfWork.Commit();

        return terminalAccount.ToAccountResponse();
    }

    private static void Validate(RequestPairTabAccountJson request)
    {
        var result = new PairTabAccountFluentValidation().Validate(request);

        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
