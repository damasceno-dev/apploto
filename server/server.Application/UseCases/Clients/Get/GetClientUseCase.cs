using server.Application.UseCases.Clients;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Clients.Get;

public class GetClientUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository)
{
    public async Task<ResponseClientJson> Execute(Guid clientId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var client = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);

        return client.ToClientResponse();
    }
}
