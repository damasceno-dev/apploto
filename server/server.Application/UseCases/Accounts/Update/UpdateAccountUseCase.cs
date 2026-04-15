using server.Communication.Requests;
using server.Communication.Responses;
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
        if (accountId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.ACCOUNT_ID_EMPTY]);
        
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var account = await accountsRepository.GetActiveByIdAndBranchId(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        account.Name = request.Name.Trim();
        account.Institution = request.Institution?.Trim();
        account.Number = request.Number?.Trim();

        await unitOfWork.Commit();

        return account.ToAccountResponse();
    }
    
    private static void Validate(RequestUpdateAccountJson request)
    {
        var result = new UpdateAccountFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
