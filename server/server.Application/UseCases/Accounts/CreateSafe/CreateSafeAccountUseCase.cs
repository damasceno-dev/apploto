using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.CreateSafe;

/// <summary>
/// <c>POST /account/safe</c> (M9.6 Phase 9): a cofre account. It never has a terminal, a paired Tab or a session;
/// a Member reaches it only through an explicit account grant.
/// </summary>
public class CreateSafeAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateAccountJson> Execute(RequestCreateSafeAccountJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var safeAccount = AccountSharedMapper.ToDomain(
            AccountType.Safe,
            authenticatedBranchUser.BranchId,
            request.Name,
            request.Institution,
            request.Number);

        await accountsRepository.Add(safeAccount);
        await unitOfWork.Commit();

        return safeAccount.ToCreateResponse();
    }

    private static void Validate(RequestCreateSafeAccountJson request)
    {
        var result = new CreateSafeAccountFluentValidation().Validate(request);

        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
