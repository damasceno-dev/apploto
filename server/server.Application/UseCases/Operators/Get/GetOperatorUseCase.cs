using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Get;

public class GetOperatorUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository)
{
    public async Task<ResponseOperatorJson> Execute(Guid operatorId)
    {
        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByIdAndBranchId(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        return op.ToOperatorResponse();
    }
}
