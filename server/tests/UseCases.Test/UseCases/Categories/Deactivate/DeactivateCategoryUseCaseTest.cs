using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Categories.Create;
using server.Application.UseCases.Categories.Deactivate;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Categories.Deactivate;

public class DeactivateCategoryUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateCategory_WhenCategoryExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(category.Id, branchUser.BranchId, category)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await useCase.Execute(category.Id);

        category.Active.ShouldBeFalse();
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdWithActiveTransactionTypes(category.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCascadeDeactivateTransactionTypes_WhenCategoryHasActiveChildren()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var tt1 = new TransactionTypeBuilder().WithActive(true).Build();
        var tt2 = new TransactionTypeBuilder().WithActive(true).Build();
        var category = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .WithTransactionTypes([tt1, tt2])
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(category.Id, branchUser.BranchId, category)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await useCase.Execute(category.Id);

        category.Active.ShouldBeFalse();
        tt1.Active.ShouldBeFalse();
        tt2.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDeactivateCategoryWithNoChildren_WhenTransactionTypesIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var category = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithActive(true)
            .WithTransactionTypes([])
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(category.Id, branchUser.BranchId, category)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await useCase.Execute(category.Id);

        category.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(Guid.NewGuid()));

        await categoriesRepository.DidNotReceive().GetActiveByIdAndBranchIdWithActiveTransactionTypes(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var categoryId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(categoryId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(categoryId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdWithActiveTransactionTypes(categoryId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenCategoryIdIsEmpty()
    {
        var authenticationService = new AuthenticationServiceBuilder().Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_ID_EMPTY);
        await categoriesRepository.DidNotReceive().GetActiveByIdAndBranchIdWithActiveTransactionTypes(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryBelongsToDifferentBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var categoryId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(categoryId, branchUser.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(categoryId));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await categoriesRepository.Received(1).GetActiveByIdAndBranchIdWithActiveTransactionTypes(categoryId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_AllowsRecreateCategoryWithSameName_AfterDeactivation()
    {
        // After deactivation (Active = false), the filtered unique index stops counting
        // that row as a name reservation. A fresh category with the same name must succeed
        // and receive a new Id — same one-way convention as M2 Account → OperatorAccount.
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var name = "Categoria Reutilizada";
        var deactivatedCategory = new CategoryBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(name)
            .WithActive(true)
            .Build();

        var deactivateAuthService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var deactivateRepo = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdWithActiveTransactionTypes(deactivatedCategory.Id, branchUser.BranchId, deactivatedCategory)
            .Build();
        var deactivateUow = new UnitOfWorkBuilder().Build();
        await new DeactivateCategoryUseCase(deactivateAuthService, deactivateRepo, deactivateUow)
            .Execute(deactivatedCategory.Id);
        deactivatedCategory.Active.ShouldBeFalse();

        // ExistsActiveByBranchIdAndName now returns false — deactivated row is excluded.
        var request = new RequestCreateCategoryJsonBuilder().WithName(name).Build();
        var createAuthService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var createRepo = new CategoriesRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, name, false)
            .Build();
        var createUow = new UnitOfWorkBuilder().Build();
        var createResponse = await new CreateCategoryUseCase(createAuthService, createRepo, createUow)
            .Execute(request);

        createResponse.Id.ShouldNotBe(deactivatedCategory.Id);
        createResponse.Name.ShouldBe(name);
        await createRepo.Received(1).Add(Arg.Is<Category>(c => c.Id != deactivatedCategory.Id && c.Name == name && c.Active));
        await createUow.Received(1).Commit();
    }

    private static DeactivateCategoryUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateCategoryUseCase(authenticationService, categoriesRepository, unitOfWork);
    }
}
