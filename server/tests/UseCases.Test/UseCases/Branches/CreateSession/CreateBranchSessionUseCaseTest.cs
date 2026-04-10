using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Branches.CreateSession;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.CreateSession;

public class CreateBranchSessionUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldIssueBranchToken_WhenUserIsActiveMember()
    {
        const string expectedToken = "branch-scoped-token";

        var authenticatedUser = new UserBuilder().Build();
        var branch = new BranchBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithUser(authenticatedUser)
            .WithBranch(branch)
            .WithRole(Role.Manager)
            .Build();
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(branch.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(branchUser)
            .Build();
        var branchesRepository = new BranchesRepositoryBuilder()
            .GetById(branch)
            .Build();
        var tokenServices = new TokenServicesBuilder()
            .GenerateBranchToken(branchUser, expectedToken)
            .Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, branchesRepository, tokenServices);

        var response = await useCase.Execute(request);

        response.Token.ShouldBe(expectedToken);
        response.Branch.Id.ShouldBe(branch.Id);
        response.Branch.Name.ShouldBe(branch.Name);
        response.Branch.Role.ShouldBe(Role.Manager);
        await branchUsersRepository.Received(1).GetActiveByUserIdAndBranchId(authenticatedUser.Id, branch.Id);
        tokenServices.Received(1).GenerateBranchToken(branchUser);
    }

    [Fact]
    public async Task Execute_ShouldThrowValidationException_WhenBranchIdIsEmpty()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(Guid.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var tokenServices = new TokenServicesBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, branchesRepository, tokenServices);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_ID_EMPTY);
        await branchUsersRepository.DidNotReceive().GetActiveByUserIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        tokenServices.DidNotReceive().GenerateBranchToken(Arg.Any<BranchUser>());
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenUserIsNotActiveMember()
    {
        var authenticatedUser = new UserBuilder().Build();
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(Guid.NewGuid())
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(null)
            .Build();
        var branchesRepository = new BranchesRepositoryBuilder().Build();
        var tokenServices = new TokenServicesBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, branchesRepository, tokenServices);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_NOT_FOUND);
        tokenServices.DidNotReceive().GenerateBranchToken(Arg.Any<BranchUser>());
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenBranchIsMissing()
    {
        var authenticatedUser = new UserBuilder().Build();
        var branch = new BranchBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithUser(authenticatedUser)
            .WithBranch(branch)
            .WithRole(Role.Admin)
            .Build();
        var request = new RequestCreateBranchSessionJsonBuilder()
            .WithBranchId(branch.Id)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetActiveByUserIdAndBranchId(branchUser)
            .Build();
        var branchesRepository = new BranchesRepositoryBuilder()
            .GetById(null)
            .Build();
        var tokenServices = new TokenServicesBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, branchesRepository, tokenServices);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_NOT_FOUND);
        tokenServices.DidNotReceive().GenerateBranchToken(Arg.Any<BranchUser>());
    }

    private static CreateBranchSessionUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IBranchUsersRepository branchUsersRepository,
        IBranchesRepository branchesRepository,
        ITokenServices tokenServices)
    {
        return new CreateBranchSessionUseCase(authenticationService, branchUsersRepository, branchesRepository, tokenServices);
    }
}
