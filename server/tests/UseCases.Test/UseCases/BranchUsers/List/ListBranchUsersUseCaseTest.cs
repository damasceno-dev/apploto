using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.BranchUsers.List;
using server.Domain.Entities.Enums;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.BranchUsers.List;

public class ListBranchUsersUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnActiveMembersFromCurrentBranch()
    {
        var currentBranch = new BranchBuilder().Build();
        var authenticatedBranchUser = new BranchUserBuilder()
            .WithBranch(currentBranch)
            .WithRole(Role.Admin)
            .Build();
        var memberOne = new BranchUserBuilder()
            .WithBranch(currentBranch)
            .WithUser(new UserBuilder().WithName("Alice").WithEmail("alice@example.com").Build())
            .WithRole(Role.Manager)
            .Build();
        var memberTwo = new BranchUserBuilder()
            .WithBranch(currentBranch)
            .WithUser(new UserBuilder().WithName("Bob").WithEmail("bob@example.com").Build())
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(authenticatedBranchUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .ListActiveByBranchId(currentBranch.Id, [memberOne, memberTwo])
            .Build();

        var useCase = new ListBranchUsersUseCase(authenticationService, branchUsersRepository);

        var response = await useCase.Execute();

        response.BranchUsers.Count.ShouldBe(2);
        response.BranchUsers[0].Name.ShouldBe("Alice");
        response.BranchUsers[0].Role.ShouldBe(Role.Manager);
        response.BranchUsers[1].Email.ShouldBe("bob@example.com");
        await branchUsersRepository.Received(1).ListActiveByBranchId(currentBranch.Id);
    }
}
