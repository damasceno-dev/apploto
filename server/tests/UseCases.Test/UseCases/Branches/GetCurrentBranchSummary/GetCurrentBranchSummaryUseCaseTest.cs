using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Branches.GetCurrentBranchSummary;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Branches.GetCurrentBranchSummary;

public class GetCurrentBranchSummaryUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldResolveCurrentBranchFromBranchToken()
    {
        var branch = new BranchBuilder().Build();
        var branchUser = new BranchUserBuilder()
            .WithBranch(branch)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var branchesRepository = new BranchesRepositoryBuilder()
            .GetById(branch.Id, branch)
            .Build();

        var useCase = new GetCurrentBranchSummaryUseCase(authenticationService, branchesRepository);

        var response = await useCase.Execute();

        response.Branch.Id.ShouldBe(branch.Id);
        response.Branch.Name.ShouldBe(branch.Name);
        response.Branch.Cnpj.ShouldBe(branch.Cnpj);
        response.Branch.Address.ShouldBe(branch.Address);
        response.Branch.Phone.ShouldBe(branch.Phone);
        response.Branch.Role.ShouldBe(Role.Admin);
        await branchesRepository.Received(1).GetById(branch.Id);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFound_WhenBranchIsMissing()
    {
        var branchUser = new BranchUserBuilder()
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var branchesRepository = new BranchesRepositoryBuilder()
            .GetById(branchUser.BranchId, null)
            .Build();

        var useCase = new GetCurrentBranchSummaryUseCase(authenticationService, branchesRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(useCase.Execute);

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_NOT_FOUND);
        await branchesRepository.Received(1).GetById(branchUser.BranchId);
    }
}
