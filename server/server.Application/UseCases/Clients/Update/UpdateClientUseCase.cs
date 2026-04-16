using server.Application.UseCases.Clients;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Clients.Update;

public class UpdateClientUseCase(
    IAuthenticationService authenticationService,
    IClientsRepository clientsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseClientJson> Execute(Guid clientId, RequestUpdateClientJson request)
    {
        Validate(clientId, request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var client = await clientsRepository.GetActiveByIdAndBranchId(clientId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);

        var normalizedCpf = ClientSharedMapper.NormalizeCpf(request.Cpf);
        if (normalizedCpf is not null)
        {
            var existing = await clientsRepository.GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId);
            if (existing is not null && existing.Id != clientId)
            {
                throw new ConflictException(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
            }
        }

        client.Name = request.Name.Trim();
        client.Phone = request.Phone.Trim();
        client.Cpf = normalizedCpf;
        client.Cep = ClientSharedMapper.NormalizeOptional(request.Cep);
        client.Address = ClientSharedMapper.NormalizeOptional(request.Address);
        client.PhoneSecondary = ClientSharedMapper.NormalizeOptional(request.PhoneSecondary);
        client.Notes = ClientSharedMapper.NormalizeOptional(request.Notes);
        client.Email = ClientSharedMapper.NormalizeOptional(request.Email);

        await unitOfWork.Commit();

        return client.ToClientResponse();
    }

    private static void Validate(Guid clientId, RequestUpdateClientJson request)
    {
        if (clientId == Guid.Empty)
        {
            throw new OnValidationException([ResourcesErrorMessages.CLIENT_ID_EMPTY]);
        }

        var result = new UpdateClientFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
