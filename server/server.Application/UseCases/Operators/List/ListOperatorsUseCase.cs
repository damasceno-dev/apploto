using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Operators.List;

public class ListOperatorsUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository)
{
    public async Task<ResponseListOperatorsJson> Execute()
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();
        var operators = await operatorsRepository.ListActiveByBranchId(authenticatedBranchUser.BranchId);

        return operators.ToResponse();
    }
}
