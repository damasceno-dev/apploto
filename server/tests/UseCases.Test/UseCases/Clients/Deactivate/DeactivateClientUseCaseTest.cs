using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Clients.Deactivate;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.Deactivate;

public class DeactivateClientUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldSetActiveFalse_WhenClientExists()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client.Id, branchUser.BranchId, client)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(client.Id);

        client.Active.ShouldBeFalse();
        response.Id.ShouldBe(client.Id);
        await clientsRepository.Received(1).GetActiveByIdAndBranchId(client.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReturnClientData_AfterDeactivation()
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
            .GetActiveByIdAndBranchId(client.Id, branchUser.BranchId, client)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(client.Id);

        response.Name.ShouldBe("Alice");
        response.BranchId.ShouldBe(branchUser.BranchId);
        await clientsRepository.Received(1).GetActiveByIdAndBranchId(client.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenClientIdIsEmpty()
    {
        var authenticationService = new AuthenticationServiceBuilder().Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_ID_EMPTY);
        await clientsRepository.DidNotReceive().GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
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
            .GetActiveByIdAndBranchId(clientId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(clientId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await clientsRepository.Received(1).GetActiveByIdAndBranchId(clientId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenClientBelongsToDifferentBranch()
    {
        // The repository scopes by (id, branchId). A client owned by branch B is
        // invisible when the authenticated user belongs to branch A — the use case
        // raises NotFoundException, preventing cross-branch mutations.
        var branchUser = new BranchUserBuilder().Build();
        var clientId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(clientId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(clientId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        // Repository was called with the authenticated branch — not a different one
        await clientsRepository.Received(1).GetActiveByIdAndBranchId(clientId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static DeactivateClientUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IClientsRepository clientsRepository,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateClientUseCase(authenticationService, clientsRepository, unitOfWork);
    }
}
