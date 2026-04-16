using server.Application.UseCases.Clients;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Clients.Create;

public static class CreateClientMapper
{
    public static Client ToDomain(this RequestCreateClientJson request, Guid branchId)
    {
        return new Client
        {
            Name = request.Name.Trim(),
            Phone = request.Phone.Trim(),
            Cpf = ClientSharedMapper.NormalizeCpf(request.Cpf),
            Cep = ClientSharedMapper.NormalizeOptional(request.Cep),
            Address = ClientSharedMapper.NormalizeOptional(request.Address),
            PhoneSecondary = ClientSharedMapper.NormalizeOptional(request.PhoneSecondary),
            Notes = ClientSharedMapper.NormalizeOptional(request.Notes),
            Email = ClientSharedMapper.NormalizeOptional(request.Email),
            BranchId = branchId
        };
    }

    extension(Client client)
    {
        public ResponseCreateClientJson ToResponse()
        {
            return new ResponseCreateClientJson
            {
                Id = client.Id,
                Name = client.Name,
                Phone = client.Phone,
                Cpf = client.Cpf,
                Cep = client.Cep,
                Address = client.Address,
                PhoneSecondary = client.PhoneSecondary,
                Notes = client.Notes,
                Email = client.Email,
                BranchId = client.BranchId
            };
        }
    }
}
