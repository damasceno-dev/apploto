using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.OperatorAccounts.SetPrimaryAccount;

public class SetPrimaryAccountUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IAccountsRepository accountsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOperatorAccountJson> Execute(Guid operatorId, Guid accountId)
    {
        Validate(operatorId, accountId);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        _ = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(accountId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        var link = await operatorAccountsRepository.GetByOperatorIdAndAccountId(operatorId, accountId);

        if (link is null || link.Active is false)
            throw new NotFoundException(ResourcesErrorMessages.OPERATOR_ACCOUNT_NOT_FOUND);

        var currentPrimary = await operatorAccountsRepository.GetActivePrimaryByOperatorId(operatorId);

        if (currentPrimary is not null && currentPrimary.Id != link.Id)
            currentPrimary.IsPrimary = false;

        link.IsPrimary = true;

        await unitOfWork.Commit();

        return link.ToResponse(account);
    }

    private static void Validate(Guid operatorId, Guid accountId)
    {
        List<string> errors = [];

        if (operatorId == Guid.Empty)
            errors.Add(ResourcesErrorMessages.OPERATOR_ID_EMPTY);

        if (accountId == Guid.Empty)
            errors.Add(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);

        if (errors.Count > 0)
            throw new OnValidationException(errors);
    }
}
