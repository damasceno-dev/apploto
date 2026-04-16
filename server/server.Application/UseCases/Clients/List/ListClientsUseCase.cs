using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Clients.List;

public class ListClientsUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository)
{
    public async Task<ResponseListClientsJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var clients = await clientsRepository.ListActiveByBranchId(branchUser.BranchId);

        return clients.ToResponse();
    }
}
