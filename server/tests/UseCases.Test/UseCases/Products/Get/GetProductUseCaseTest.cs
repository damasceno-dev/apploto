using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Products.Get;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Products.Get;

public class GetProductUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnProduct_WhenProductExists()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(product.Id, branchUser.BranchId, product)
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        var response = await useCase.Execute(product.Id);

        response.Id.ShouldBe(product.Id);
        response.Name.ShouldBe(product.Name);
        response.BranchId.ShouldBe(branchUser.BranchId);
        await productsRepository.Received(1).GetActiveByIdAndBranchIdAsNoTracking(product.Id, branchUser.BranchId);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenProductDoesNotExist()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var productId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(productId, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(productId));

        exception.Message.ShouldBe(ResourcesErrorMessages.PRODUCT_NOT_FOUND);
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenProductBelongsToOtherBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var otherBranchId = Guid.NewGuid();
        var product = new ProductBuilder().WithBranchId(otherBranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByIdAndBranchIdAsNoTracking(product.Id, branchUser.BranchId, null)
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(product.Id));
    }

    private static GetProductUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IProductsRepository productsRepository)
    {
        return new GetProductUseCase(authenticationService, productsRepository);
    }
}
