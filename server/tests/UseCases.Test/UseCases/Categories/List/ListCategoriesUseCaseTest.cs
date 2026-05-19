using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Categories.List;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Categories.List;

public class ListCategoriesUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnCategoriesForAuthenticatedBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var c1 = new CategoryBuilder().WithBranchId(branchUser.BranchId).WithName("Alpha").Build();
        var c2 = new CategoryBuilder().WithBranchId(branchUser.BranchId).WithName("Beta").Build();
        IReadOnlyList<server.Domain.Entities.Category> list = [c1, c2];

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, list)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var response = await useCase.Execute();

        response.Items.Count.ShouldBe(2);
        response.Items.ShouldContain(c => c.Id == c1.Id && c.Name == c1.Name);
        response.Items.ShouldContain(c => c.Id == c2.Id && c.Name == c2.Name);
        await categoriesRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenBranchHasNoCategories()
    {
        var branchUser = new BranchUserBuilder().Build();
        IReadOnlyList<server.Domain.Entities.Category> emptyList = [];

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, emptyList)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var response = await useCase.Execute();

        response.Items.ShouldBeEmpty();
        await categoriesRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldNotReturnCategoriesFromOtherBranches()
    {
        var branchUser = new BranchUserBuilder().Build();
        var otherBranchId = Guid.NewGuid();
        var otherCategory = new CategoryBuilder().WithBranchId(otherBranchId).Build();
        IReadOnlyList<server.Domain.Entities.Category> myList = [];

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, myList)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var response = await useCase.Execute();

        response.Items.ShouldNotContain(c => c.Id == otherCategory.Id);
        await categoriesRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
        await categoriesRepository.DidNotReceive().ListActiveByBranchIdAsNoTracking(otherBranchId);
    }

    private static ListCategoriesUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository)
    {
        return new ListCategoriesUseCase(authenticationService, categoriesRepository);
    }
}
