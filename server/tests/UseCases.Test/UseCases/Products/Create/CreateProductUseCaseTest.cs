using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.Products.Create;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Products.Create;

public class CreateProductUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldCreateProduct_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateProductJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.Name.ShouldBe(request.Name.Trim());
        response.DisplayOrder.ShouldBe(request.DisplayOrder);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await productsRepository.Received(1).ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), null);
        await productsRepository.Received(1).Add(Arg.Is<Product>(p =>
            p.Name == request.Name.Trim() &&
            p.DisplayOrder == request.DisplayOrder &&
            p.BranchId == branchUser.BranchId &&
            p.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldCreateProduct_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var request = new RequestCreateProductJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchId.ShouldBe(branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var request = new RequestCreateProductJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request));

        await productsRepository.DidNotReceive().Add(Arg.Any<Product>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateProductJsonBuilder().WithName("Existing Name").Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "Existing Name", true)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);
        await productsRepository.DidNotReceive().Add(Arg.Any<Product>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameIsDiferencaCaixa()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);
        await productsRepository.DidNotReceive().Add(Arg.Any<Product>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNameIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateProductJsonBuilder().WithName(string.Empty).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_NAME_REQUIRED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenDisplayOrderIsNegative()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestCreateProductJsonBuilder().WithDisplayOrder(-1).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_DISPLAY_ORDER_INVALID);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static CreateProductUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IProductsRepository productsRepository,
        IUnitOfWork unitOfWork)
    {
        return new CreateProductUseCase(authenticationService, productsRepository, unitOfWork);
    }
}
