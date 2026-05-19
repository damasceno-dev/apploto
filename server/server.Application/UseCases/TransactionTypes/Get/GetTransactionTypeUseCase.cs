using server.Application.UseCases.TransactionTypes;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Get;

public class GetTransactionTypeUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository)
{
    public async Task<ResponseTransactionTypeJson> Execute(Guid transactionTypeId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var transactionType = await transactionTypesRepository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(
            transactionTypeId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        return transactionType.ToResponse();
    }
}
