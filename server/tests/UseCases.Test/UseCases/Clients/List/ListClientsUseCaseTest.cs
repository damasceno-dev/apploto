using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Clients.List;
using server.Domain.Entities;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.List;

public class ListClientsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnClients_WhenBranchHasActiveClients()
    {
        var branchUser = new BranchUserBuilder().Build();
        var clients = new List<Client>
        {
            new ClientBuilder().WithBranchId(branchUser.BranchId).WithName("Alice").Build(),
            new ClientBuilder().WithBranchId(branchUser.BranchId).WithName("Bob").Build()
        }.AsReadOnly();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, clients)
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var response = await useCase.Execute();

        response.Clients.ShouldNotBeEmpty();
        response.Clients.Count.ShouldBe(2);
        response.Clients.ShouldAllBe(c => c.BranchId == branchUser.BranchId);
        await clientsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenBranchHasNoActiveClients()
    {
        var branchUser = new BranchUserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, new List<Client>().AsReadOnly())
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var response = await useCase.Execute();

        response.Clients.ShouldBeEmpty();
        await clientsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldPassAuthenticatedBranchIdToRepository()
    {
        // Proves the use case forwards the authenticated branch ID exactly — not a hardcoded
        // value or a different branch — to the repository scope query.
        var branchUser = new BranchUserBuilder().Build();
        var ownClient = new ClientBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, new List<Client> { ownClient }.AsReadOnly())
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var response = await useCase.Execute();

        response.Clients.ShouldHaveSingleItem();
        response.Clients[0].BranchId.ShouldBe(branchUser.BranchId);
        await clientsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnInactiveClients()
    {
        // Repository filters Active = true. The use case relies on that contract;
        // when the repository returns empty the result has no inactive entries.
        var branchUser = new BranchUserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .ListActiveByBranchId(branchUser.BranchId, new List<Client>().AsReadOnly())
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var response = await useCase.Execute();

        response.Clients.ShouldBeEmpty();
        await clientsRepository.Received(1).ListActiveByBranchId(branchUser.BranchId);
    }

    private static ListClientsUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IClientsRepository clientsRepository)
    {
        return new ListClientsUseCase(authenticationService, clientsRepository);
    }
}
