using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using CommonTestUtilities.Requests;
using server.Application.UseCases.Products.Create;
using server.Application.UseCases.Products.Deactivate;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Products.Deactivate;

public class DeactivateProductUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldDeactivateProduct_WhenProductExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var cashVarianceId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        await useCase.Execute(product.Id);

        product.Active.ShouldBeFalse();
        await productsRepository.Received(1).GetActiveByIdAndBranchId(product.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldDeactivateProduct_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var cashVarianceId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        await useCase.Execute(product.Id);

        product.Active.ShouldBeFalse();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var resolver = new CashVarianceProductResolverBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(product.Id));

        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenProductNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var productId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(productId, branchUser.BranchId, null)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, Guid.NewGuid())
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(productId));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenDeactivatingSystemProduct()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, product.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(product.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_SYSTEM_PROTECTED);
        product.Active.ShouldBeTrue();
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenProductIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var resolver = new CashVarianceProductResolverBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_RecreateAfterDeactivate_ShouldAllowNewRowWithSameName()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var originalProduct = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Raspadinha")
            .Build();
        var cashVarianceId = Guid.NewGuid();
        var sharedUnitOfWork = new UnitOfWorkBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();

        // Phase 1: deactivate the original product
        var deactivateRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(originalProduct.Id, branchUser.BranchId, originalProduct)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();

        var deactivateUseCase = CreateUseCase(authenticationService, deactivateRepository, resolver, sharedUnitOfWork);
        await deactivateUseCase.Execute(originalProduct.Id);

        originalProduct.Active.ShouldBeFalse();

        // Phase 2: re-create with same name — filtered unique index frees the name slot
        var createRequest = new RequestCreateProductJsonBuilder().WithName("Raspadinha").Build();
        var createRepository = new ProductsRepositoryBuilder()
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "Raspadinha", false)
            .Build();

        var createUseCase = new CreateProductUseCase(authenticationService, createRepository, sharedUnitOfWork);
        var response = await createUseCase.Execute(createRequest);

        response.Id.ShouldNotBe(originalProduct.Id);
        response.Name.ShouldBe("Raspadinha");
        response.BranchId.ShouldBe(branchUser.BranchId);
        await sharedUnitOfWork.Received(2).Commit();
    }

    private static DeactivateProductUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IProductsRepository productsRepository,
        ICashVarianceProductResolver resolver,
        IUnitOfWork unitOfWork)
    {
        return new DeactivateProductUseCase(authenticationService, productsRepository, resolver, unitOfWork);
    }
}
