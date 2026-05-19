using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.TransactionTypes.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TransactionTypes.Create;

public class CreateTransactionTypeUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateTransactionType_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .Build();
        var createdTt = new TransactionTypeBuilder()
            .WithName(request.Name.Trim())
            .WithCategory(category)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ExistsActiveByCategoryIdAndName(category.Id, request.Name.Trim(), false)
            .Build();
        ttRepository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Arg.Any<Guid>(), branchUser.BranchId)
            .Returns(createdTt);
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name.Trim());
        response.CategoryId.ShouldBe(category.Id);
        response.CategoryName.ShouldBe(category.Name);
        await ttRepository.Received(1).Add(Arg.Is<TransactionType>(tt =>
            tt.Name == request.Name.Trim() &&
            tt.CategoryId == category.Id &&
            tt.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTransactionType_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder().WithCategoryId(category.Id).Build();
        var createdTt = new TransactionTypeBuilder().WithCategory(category).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ExistsActiveByCategoryIdAndName(category.Id, request.Name.Trim(), false)
            .Build();
        ttRepository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Arg.Any<Guid>(), branchUser.BranchId)
            .Returns(createdTt);
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        await Should.NotThrowAsync(() => useCase.Execute(request));

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request));

        await categoriesRepository.DidNotReceive().GetActiveByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenCategoryNotInBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var foreignCategoryId = Guid.NewGuid();
        var request = new RequestCreateTransactionTypeJsonBuilder().WithCategoryId(foreignCategoryId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(foreignCategoryId, branchUser.BranchId, null)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NOT_FOUND);
        await ttRepository.DidNotReceive().Add(Arg.Any<TransactionType>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyExistsInCategory()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("Conflicting Name")
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ExistsActiveByCategoryIdAndName(category.Id, "Conflicting Name", true)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT);
        await ttRepository.DidNotReceive().Add(Arg.Any<TransactionType>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateTransactionType_WhenSameNameExistsInDifferentCategory()
    {
        // Uniqueness is per-category, not per-branch — same name in a different category is allowed.
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var category = new CategoryBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder()
            .WithCategoryId(category.Id)
            .WithName("Shared Name")
            .Build();
        var createdTt = new TransactionTypeBuilder().WithName("Shared Name").WithCategory(category).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(category.Id, branchUser.BranchId, category)
            .Build();
        // ExistsActiveByCategoryIdAndName returns false for THIS category — the same name in a different category is irrelevant.
        var ttRepository = new TransactionTypesRepositoryBuilder()
            .ExistsActiveByCategoryIdAndName(category.Id, "Shared Name", false)
            .Build();
        ttRepository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(Arg.Any<Guid>(), branchUser.BranchId)
            .Returns(createdTt);
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe("Shared Name");
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateTransactionTypeJsonBuilder().WithName(string.Empty).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var ttRepository = new TransactionTypesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, ttRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_TYPE_NAME_REQUIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateTransactionTypeUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository,
        ITransactionTypesRepository transactionTypesRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateTransactionTypeUseCase(
            authenticationService, categoriesRepository, transactionTypesRepository, unitOfWork);
    }
}
