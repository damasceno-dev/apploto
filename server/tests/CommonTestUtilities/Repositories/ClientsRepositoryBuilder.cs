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

    public ClientsRepositoryBuilder GetActiveByIdAndBranchId(Guid id, Guid branchId, Client? client)
    {
        _repository.GetActiveByIdAndBranchId(id, branchId).Returns(client);
        return this;
    }

    public ClientsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, Client? client)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(id, branchId).Returns(client);
        return this;
    }

    public ClientsRepositoryBuilder ListActiveByBranchId(Guid branchId, IReadOnlyList<Client> clients)
    {
        _repository.ListActiveByBranchId(branchId).Returns(clients);
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
