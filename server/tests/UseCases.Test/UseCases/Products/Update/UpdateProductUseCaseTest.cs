using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.Products.Update;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Products.Update;

public class UpdateProductUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateProduct_WhenValidRequest()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).WithName("Old Name").Build();
        var request = new RequestUpdateProductJsonBuilder().WithName("New Name").WithDisplayOrder(5).Build();
        var cashVarianceId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "New Name", false, product.Id)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var response = await useCase.Execute(product.Id, request);

        product.Name.ShouldBe("New Name");
        product.DisplayOrder.ShouldBe(5);
        response.Name.ShouldBe("New Name");
        response.DisplayOrder.ShouldBe(5);
        await productsRepository.Received(1).GetActiveByIdAndBranchId(product.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldUpdateProduct_WhenManagerRole()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateProductJsonBuilder().Build();
        var cashVarianceId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, request.Name.Trim(), false, product.Id)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        await useCase.Execute(product.Id, request);

        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermissionException_WhenCallerIsMember()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();
        var request = new RequestUpdateProductJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var resolver = new CashVarianceProductResolverBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(product.Id, request));

        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenProductNotFound()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var productId = Guid.NewGuid();
        var request = new RequestUpdateProductJsonBuilder().Build();

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

        await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(productId, request));

        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowConflictException_WhenNameAlreadyExistsOnAnotherProduct()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).WithName("Old Name").Build();
        var request = new RequestUpdateProductJsonBuilder().WithName("Conflicting Name").Build();
        var cashVarianceId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, "Conflicting Name", true, product.Id)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, cashVarianceId)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(product.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_NAME_CONFLICT);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenRenamingSystemProduct()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();
        var request = new RequestUpdateProductJsonBuilder().WithName("Renamed Name").Build();

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

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(product.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_SYSTEM_PROTECTED);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenNormalProductTakesReservedName()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName("Soda")
            .Build();
        var request = new RequestUpdateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();
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

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(product.Id, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_SYSTEM_PROTECTED);
        await productsRepository.DidNotReceive().ExistsActiveByBranchIdAndName(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<Guid?>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowDisplayOrderChangeOnSystemProduct()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder()
            .WithBranchId(branchUser.BranchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .WithDisplayOrder(1)
            .Build();
        var request = new RequestUpdateProductJsonBuilder()
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .WithDisplayOrder(99)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchId(product.Id, branchUser.BranchId, product)
            .ExistsActiveByBranchIdAndName(branchUser.BranchId, CashVarianceProductResolver.CashVarianceProductName, false, product.Id)
            .Build();
        var resolver = new CashVarianceProductResolverBuilder()
            .ReturnsId(branchUser.BranchId, product.Id)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var response = await useCase.Execute(product.Id, request);

        product.DisplayOrder.ShouldBe(99);
        product.Name.ShouldBe(CashVarianceProductResolver.CashVarianceProductName);
        response.DisplayOrder.ShouldBe(99);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenProductIdIsEmpty()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var request = new RequestUpdateProductJsonBuilder().Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder().Build();
        var resolver = new CashVarianceProductResolverBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, productsRepository, resolver, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.PRODUCT_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateProductUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IProductsRepository productsRepository,
        ICashVarianceProductResolver resolver,
        IUnitOfWork unitOfWork)
    {
        return new UpdateProductUseCase(authenticationService, productsRepository, resolver, unitOfWork);
    }
}
