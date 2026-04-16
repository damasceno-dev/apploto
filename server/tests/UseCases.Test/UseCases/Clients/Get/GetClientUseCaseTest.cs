using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Clients.Get;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.Get;

public class GetClientUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnClient_WhenFoundInAuthenticatedBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Alice")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(client.Id, branchUser.BranchId, client)
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var response = await useCase.Execute(client.Id);

        response.Id.ShouldBe(client.Id);
        response.Name.ShouldBe(client.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await clientsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(client.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenClientNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var clientId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(clientId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await clientsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenClientBelongsToDifferentBranch()
    {
        // The repository scopes by (id, branchId). A client that exists in branch B is
        // invisible when queried with branch A's id — the repository returns null and the
        // use case raises NotFoundException, preventing cross-branch data leakage.
        var branchUser = new BranchUserBuilder().Build();
        var clientId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(clientId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        // Repository was called with the authenticated branch — not a different branch
        await clientsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(clientId, branchUser.BranchId);
    }

    private static GetClientUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IClientsRepository clientsRepository)
    {
        return new GetClientUseCase(authenticationService, clientsRepository);
    }
}
