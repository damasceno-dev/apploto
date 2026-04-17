using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Clients;
using server.Application.UseCases.Clients.Create;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Clients.Create;

public class CreateClientUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateClient_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder().Build();
        var normalizedCpf = ClientSharedMapper.NormalizeCpf(request.Cpf);
        normalizedCpf.ShouldNotBeNull();
        
        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name.Trim());
        response.Phone.ShouldBe(request.Phone.Trim());
        response.BranchId.ShouldBe(branchUser.BranchId);
        await clientsRepository.Received(1).GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId);
        await clientsRepository.Received(1).Add(Arg.Is<Client>(c =>
            c.Name == request.Name.Trim() &&
            c.Phone == request.Phone.Trim() &&
            c.BranchId == branchUser.BranchId &&
            c.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateClient_WhenCpfIsNull()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Cpf.ShouldBeNull();
        await clientsRepository.DidNotReceive().GetActiveByCpfAndBranchId(Arg.Any<string>(), Arg.Any<Guid>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldStoreCpfNormalized_WhenFormattedCpfProvided()
    {
        // Formatted "123.456.789-01" must be stored as normalized digits "12345678901".
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("123.456.789-01")
            .Build();
        var normalizedCpf = ClientSharedMapper.NormalizeCpf(request.Cpf);
        normalizedCpf.ShouldNotBeNull();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Cpf.ShouldBe(normalizedCpf);
        await clientsRepository.Received(1).GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenCpfAlreadyExistsInBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var existingClient = new ClientBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithCpf("12345678901")
            .Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("123.456.789-01")  // same CPF, formatted; normalizes to "12345678901"
            .Build();
        var normalizedCpf = ClientSharedMapper.NormalizeCpf(request.Cpf);
        normalizedCpf.ShouldNotBeNull();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder()
            .GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId, existingClient)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
        await clientsRepository.Received(1).GetActiveByCpfAndBranchId(normalizedCpf, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenPhoneIsEmpty()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithPhone(string.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_PHONE_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenCpfDoesNotHave11Digits()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("1234567890")  // 10 digits — one short
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_CPF_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenEmailIsInvalid()
    {
        var branchUser = new BranchUserBuilder().Build();
        var request = new RequestCreateClientJsonBuilder()
            .WithEmail("not-an-email")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var clientsRepository = new ClientsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, clientsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.EMAIL_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateClientUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IClientsRepository clientsRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateClientUseCase(authenticationService, clientsRepository, unitOfWork);
    }
}
