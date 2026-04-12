using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Deactivate;

public class DeactivateOperatorUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOperatorJson> Execute(Guid operatorId)
    {
        Validate(operatorId);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByIdAndBranchId(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        op.Active = false;
        await unitOfWork.Commit();

        return op.ToOperatorResponse();
    }

    private static void Validate(Guid operatorId)
    {
        if (operatorId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.OPERATOR_ID_EMPTY]);
        }
    }
}
