using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Operators.Deactivate;

public class DeactivateOperatorUseCase(
    IAuthenticationService authenticationService,
    IOperatorsRepository operatorsRepository,
    IOperatorAccountsRepository operatorAccountsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseOperatorJson> Execute(Guid operatorId)
    {
        if (operatorId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.OPERATOR_ID_EMPTY]);

        var authenticatedBranchUser = await authenticationService.GetAuthenticatedBranchUser();

        var op = await operatorsRepository.GetActiveByIdAndBranchId(operatorId, authenticatedBranchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        var activeLinks = await operatorAccountsRepository.ListActiveByOperatorId(op.Id);

        foreach (var link in activeLinks)
        {
            link.Active = false;
            link.IsPrimary = false;
        }

        op.Active = false;

        await unitOfWork.Commit();

        return op.ToOperatorResponse();
    }
}
