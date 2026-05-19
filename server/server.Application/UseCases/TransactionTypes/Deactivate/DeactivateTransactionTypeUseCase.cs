using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Deactivate;

public class DeactivateTransactionTypeUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository,
    IUnitOfWork unitOfWork)
{
    public async Task Execute(Guid transactionTypeId)
    {
        if (transactionTypeId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_TYPE_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var transactionType = await transactionTypesRepository.GetActiveByIdWithCategoryAndBranchId(
            transactionTypeId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        transactionType.Active = false;

        await unitOfWork.Commit();
    }
}
