using server.Application.UseCases.Clients;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Clients.Create;

public class CreateClientUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateClientJson> Execute(RequestCreateClientJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var normalizedCpf = ClientSharedMapper.NormalizeCpf(request.Cpf);
        if (normalizedCpf is not null)
        {
            var existing = await clientsRepository.GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId);
            if (existing is not null)
            {
                throw new ConflictException(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
            }
        }

        var client = request.ToDomain(branchUser.BranchId);

        await clientsRepository.Add(client);
        await unitOfWork.Commit();

        return client.ToResponse();
    }

    private static void Validate(RequestCreateClientJson request)
    {
        var result = new CreateClientFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
