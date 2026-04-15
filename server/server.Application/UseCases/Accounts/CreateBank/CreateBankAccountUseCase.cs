using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Accounts.CreateBank;

public class CreateBankAccountUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateAccountJson> Execute(RequestCreateBankAccountJson request)
    {
        Validate(request);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var bankAccount = AccountSharedMapper.ToDomain(
            AccountType.BankAccount,
            authenticatedBranchUser.BranchId,
            request.Name,
            request.Institution,
            request.Number);

        await accountsRepository.Add(bankAccount);
        await unitOfWork.Commit();

        return bankAccount.ToCreateResponse();
    }

    private static void Validate(RequestCreateBankAccountJson request)
    {
        var result = new CreateBankAccountFluentValidation().Validate(request);

        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
