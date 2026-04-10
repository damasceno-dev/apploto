using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Branches.ListMyBranches;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.ListMyBranches;

public class ListMyBranchesUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnAuthenticatedUserActiveMemberships()
    {
        var authenticatedUser = new UserBuilder().Build();
        var adminBranch = new BranchBuilder().WithName("Admin Branch").Build();
        var managerBranch = new BranchBuilder().WithName("Manager Branch").Build();
        var adminMembership = new BranchUserBuilder()
            .WithUser(authenticatedUser)
            .WithBranch(adminBranch)
            .WithRole(Role.Admin)
            .Build();
        var managerMembership = new BranchUserBuilder()
            .WithUser(authenticatedUser)
            .WithBranch(managerBranch)
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .ListActiveByUserId([adminMembership, managerMembership])
            .Build();

        var useCase = new ListMyBranchesUseCase(authenticationService, branchUsersRepository);

        var response = await useCase.Execute();

        response.Branches.Count.ShouldBe(2);
        response.Branches[0].Id.ShouldBe(adminBranch.Id);
        response.Branches[0].Name.ShouldBe(adminBranch.Name);
        response.Branches[0].Role.ShouldBe(Role.Admin);
        response.Branches[1].Id.ShouldBe(managerBranch.Id);
        response.Branches[1].Name.ShouldBe(managerBranch.Name);
        response.Branches[1].Role.ShouldBe(Role.Manager);
        await branchUsersRepository.Received(1).ListActiveByUserId(authenticatedUser.Id);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenUserHasNoActiveMemberships()
    {
        var authenticatedUser = new UserBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedUser(authenticatedUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .ListActiveByUserId([])
            .Build();

        var useCase = new ListMyBranchesUseCase(authenticationService, branchUsersRepository);

        var response = await useCase.Execute();

        response.Branches.ShouldBeEmpty();
        await branchUsersRepository.Received(1).ListActiveByUserId(authenticatedUser.Id);
    }
}
