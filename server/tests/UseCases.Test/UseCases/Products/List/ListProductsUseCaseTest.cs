using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.Products.List;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.Products.List;

public class ListProductsUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldReturnActiveProducts_OrderedByDisplayOrder()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product1 = new ProductBuilder().WithBranchId(branchUser.BranchId).WithDisplayOrder(1).Build();
        var product2 = new ProductBuilder().WithBranchId(branchUser.BranchId).WithDisplayOrder(2).Build();
        var products = new List<server.Domain.Entities.Product> { product1, product2 };

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, products)
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        var response = await useCase.Execute();

        response.Items.ShouldNotBeEmpty();
        response.Items.Count.ShouldBe(2);
        response.Items[0].Id.ShouldBe(product1.Id);
        response.Items[1].Id.ShouldBe(product2.Id);
    }

    [Fact]
    public async Task Execute_ShouldReturnEmptyList_WhenNoProducts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, [])
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        var response = await useCase.Execute();

        response.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Execute_ShouldOnlyReturnProductsFromAuthenticatedBranch()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var product = new ProductBuilder().WithBranchId(branchUser.BranchId).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .ListActiveByBranchIdAsNoTracking(branchUser.BranchId, [product])
            .Build();

        var useCase = CreateUseCase(authenticationService, productsRepository);

        var response = await useCase.Execute();

        response.Items.ShouldAllBe(p => p.BranchId == branchUser.BranchId);
        await productsRepository.Received(1).ListActiveByBranchIdAsNoTracking(branchUser.BranchId);
    }

    private static ListProductsUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IProductsRepository productsRepository)
    {
        return new ListProductsUseCase(authenticationService, productsRepository);
    }
}
