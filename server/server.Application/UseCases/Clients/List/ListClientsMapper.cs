using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Clients.List;

public static class ListClientsMapper
{
    extension(IEnumerable<Client> clients)
    {
        public ResponseListClientsJson ToResponse()
        {
            return new ResponseListClientsJson
            {
                Clients = clients
                    .Select(c => c.ToClientResponse())
                    .ToList()
            };
        }
    }
}
