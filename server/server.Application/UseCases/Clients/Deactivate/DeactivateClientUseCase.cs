using server.Application.UseCases.Clients;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Clients.Deactivate;

public class DeactivateClientUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseClientJson> Execute(Guid clientId)
    {
        if (clientId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.CLIENT_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var client = await clientsRepository.GetActiveByIdAndBranchId(clientId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);

        client.Active = false;

        await unitOfWork.Commit();

        return client.ToClientResponse();
    }
}
