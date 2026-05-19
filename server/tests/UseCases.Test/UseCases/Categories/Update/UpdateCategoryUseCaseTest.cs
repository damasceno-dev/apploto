using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Categories.Update;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Categories.Update;

public class UpdateCategoryUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateCategory_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Old Name")
            .Build();
        var request = new RequestUpdateCategoryJsonBuilder().WithName("New Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchId(category.Id, branchUser.BranchId, category)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "New Name", false, category.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var response = await useCase.Execute(category.Id, request);

        category.Name.ShouldBe("New Name");
        response.Name.ShouldBe("New Name");
        response.BranchId.ShouldBe(branchUser.BranchId);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchId(category.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateCategory_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchId(category.Id, branchUser.BranchId, category)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false, category.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await Should.NotThrowAsync(() => useCase.Execute(category.Id, request));

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var categoryId = Guid.NewGuid();
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(categoryId, request));

        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var categoryId = Guid.NewGuid();
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchId(categoryId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(categoryId, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchId(categoryId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyUsedByAnotherCategory()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateCategoryJsonBuilder().WithName("Taken Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchId(category.Id, branchUser.BranchId, category)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "Taken Name", true, category.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(category.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenCategoryIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateCategoryJsonBuilder().WithName(string.Empty).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.NewGuid(), request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateCategoryUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateCategoryUseCase(authenticationService, categoriesRepository, unitOfWork);
    }
}
