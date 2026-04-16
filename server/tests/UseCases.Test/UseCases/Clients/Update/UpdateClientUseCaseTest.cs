using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Clients.Update;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.Update;

public class UpdateClientUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateClient_WhenValidRequest()
    {
        // Default request builder includes a formatted CPF. NSubstitute returns null for the
        // uniqueness check (no conflict configured), so the happy path completes normally.
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Old Name")
            .WithPhone("11111111111")
            .Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithName("New Name")
            .WithPhone("22222222222")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(client.Id, request);

        client.Name.ShouldBe("New Name");
        client.Phone.ShouldBe("22222222222");
        response.Name.ShouldBe("New Name");
        response.Phone.ShouldBe("22222222222");
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldClearCpf_WhenCpfIsNull()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithCpf("12345678901")
            .Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(client.Id, request);

        client.Cpf.ShouldBeNull();
        response.Cpf.ShouldBeNull();
        await clientsRepository.DidNotReceive().GetActiveByCpfAndBranchId(Arg.Any<string>(), Arg.Any<Guid>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldStoreCpfNormalized_WhenFormattedCpfProvided()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf("123.456.789-01")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client)
            .GetActiveByCpfAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(client.Id, request);

        client.Cpf.ShouldBe("12345678901");
        response.Cpf.ShouldBe("12345678901");
        await clientsRepository.Received(1).GetActiveByCpfAndBranchId("12345678901", branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldNotThrowConflict_WhenCpfBelongsToSameClient()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithCpf("12345678901")
            .Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf("123.456.789-01")  // same CPF formatted; normalizes to same digits
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client)
            .GetActiveByCpfAndBranchId(client)  // same client owns this normalized CPF
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        await Should.NotThrowAsync(() => useCase.Execute(client.Id, request));

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenCpfBelongsToAnotherClient()
    {
        var branchUser = new BranchUserBuilder().Build();
        var client = new ClientBuilder().WithBranchId(branchUser.BranchId).Build();
        var anotherClient = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithCpf("12345678901")
            .Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf("123.456.789-01")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(client)
            .GetActiveByCpfAndBranchId(anotherClient)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(client.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenClientNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateClientJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByIdAndBranchId(null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenClientIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateClientJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenCpfDoesNotHave11Digits()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf("1234567890")  // 10 digits — one short
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_CPF_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenEmailIsInvalid()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestUpdateClientJsonBuilder()
            .WithEmail("not-an-email")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.EMAIL_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateClientUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IClientsRepository clientsRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateClientUseCase(authenticationService, clientsRepository, unitOfWork);
    }
}
