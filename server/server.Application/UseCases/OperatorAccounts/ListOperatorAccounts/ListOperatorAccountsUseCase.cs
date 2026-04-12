using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.OperatorAccounts.ListOperatorAccounts;

public class ListOperatorAccountsUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository)
{
    public async Task<ResponseListOperatorAccountsJson> Execute(Guid operatorId)
    {
        if (operatorId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.OPERATOR_ID_EMPTY]);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        _ = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        var links = await operatorAccountsRepository.ListActiveByOperatorIdWithAccount(operatorId);

        return links.ToListResponse();
    }
}
