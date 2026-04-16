using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class ClientsRepositoryBuilder
{
    private readonly IClientsRepository _repository = Substitute.For<IClientsRepository>();

    public ClientsRepositoryBuilder GetActiveByIdAndBranchId(Client? client)
    {
        _repository.GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(client);
        return this;
    }

    public ClientsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Client? client)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(client);
        return this;
    }

    public ClientsRepositoryBuilder ListActiveByBranchId(IReadOnlyList<Client> clients)
    {
        _repository.ListActiveByBranchId(Arg.Any<Guid>()).Returns(clients);
        return this;
    }

    public ClientsRepositoryBuilder GetActiveByCpfAndBranchId(Client? client)
    {
        _repository.GetActiveByCpfAndBranchId(Arg.Any<string>(), Arg.Any<Guid>()).Returns(client);
        return this;
    }

    public IClientsRepository Build()
    {
        return _repository;
    }
}
