using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Categories.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Categories.Create;

public class CreateCategoryUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateCategory_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name.Trim());
        response.DefaultDirection.ShouldBe(request.DefaultDirection);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await categoriesRepository.Received(1).ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), null);
        await categoriesRepository.Received(1).Add(Arg.Is<Category>(c =>
            c.Name == request.Name.Trim() &&
            c.DefaultDirection == request.DefaultDirection &&
            c.BranchId == branchUser.BranchId &&
            c.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateCategory_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchId.ShouldBe(branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestCreateCategoryJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request));

        await categoriesRepository.DidNotReceive().ExistsActiveByBranchIdAndName(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyExistsInBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateCategoryJsonBuilder().WithName("Existing Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "Existing Name", true)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.CATEGORY_NAME_CONFLICT);
        await categoriesRepository.DidNotReceive().Add(Arg.Any<Category>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateCategoryJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_NAME_REQUIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenDirectionIsInvalid()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateCategoryJsonBuilder()
            .WithDefaultDirection((Direction)99)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var categoriesRepository = new CategoriesRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, categoriesRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.CATEGORY_DEFAULT_DIRECTION_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateCategoryUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        ICategoriesRepository categoriesRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateCategoryUseCase(authenticationService, categoriesRepository, unitOfWork);
    }
}
