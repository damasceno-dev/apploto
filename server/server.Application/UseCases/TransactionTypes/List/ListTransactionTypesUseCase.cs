using server.Application.UseCases.TransactionTypes;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.TransactionTypes.List;

public class ListTransactionTypesUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository)
{
    public async Task<ResponseListTransactionTypesJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var transactionTypes = await transactionTypesRepository.ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        return transactionTypes.ToListResponse();
    }
}
