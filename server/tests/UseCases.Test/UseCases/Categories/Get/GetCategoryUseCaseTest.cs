using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Categories.Get;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Categories.Get;

public class GetCategoryUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnCategory_WhenFoundInAuthenticatedBranch()
    {
        var branchUser = new BranchUserBuilder().Build();
        var category = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("My Category")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var response = await useCase.Execute(category.Id);

        response.Id.ShouldBe(category.Id);
        response.Name.ShouldBe(category.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryNotFound()
    {
        var branchUser = new BranchUserBuilder().Build();
        var categoryId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(categoryId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(categoryId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(categoryId, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryBelongsToDifferentBranch()
    {
        // Repository scopes by (id, branchId) — a category from another branch is invisible,
        // so the use case raises NotFoundException, preventing cross-branch data leakage.
        var branchUser = new BranchUserBuilder().Build();
        var categoryId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(categoryId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(categoryId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(categoryId, branchUser.BranchId);
    }

    private static GetCategoryUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository)
    {
        return new GetCategoryUseCase(authenticationService, categoriesRepository);
    }
}
