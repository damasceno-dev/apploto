using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.DailyCloses;
using server.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.DailyCloses;

public class CashVarianceProductResolverTest
{
    [Fact]
    public async Task GetIdAsync_ShouldReturnProductId_WhenSeedExists()
    {
        var branchId = Guid.NewGuid();
        var product = new ProductBuilder()
            .WithBranchId(branchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByBranchIdAndNameAsNoTrackingReturns(
                branchId,
                CashVarianceProductResolver.CashVarianceProductName,
                product)
            .Build();
        var resolver = new CashVarianceProductResolver(productsRepository);

        var result = await resolver.GetIdAsync(branchId);

        result.ShouldBe(product.Id);
    }

    [Fact]
    public async Task GetIdAsync_ShouldThrowInvalidOperationException_WhenSeedIsMissing()
    {
        var branchId = Guid.NewGuid();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByBranchIdAndNameAsNoTrackingReturns(
                branchId,
                CashVarianceProductResolver.CashVarianceProductName,
                null)
            .Build();
        var resolver = new CashVarianceProductResolver(productsRepository);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => resolver.GetIdAsync(branchId));

        ex.Message.ShouldContain(branchId.ToString());
        ex.Message.ShouldContain(CashVarianceProductResolver.CashVarianceProductName);
    }

    [Fact]
    public async Task GetIdAsync_ShouldPassExactBranchIdAndNameToRepository()
    {
        var branchId = Guid.NewGuid();
        var product = new ProductBuilder()
            .WithBranchId(branchId)
            .WithName(CashVarianceProductResolver.CashVarianceProductName)
            .Build();
        var productsRepository = new ProductsRepositoryBuilder()
            .GetActiveByBranchIdAndNameAsNoTrackingReturns(
                branchId,
                CashVarianceProductResolver.CashVarianceProductName,
                product)
            .Build();
        var resolver = new CashVarianceProductResolver(productsRepository);

        await resolver.GetIdAsync(branchId);

        await productsRepository.Received(1).GetActiveByBranchIdAndNameAsNoTracking(
            Arg.Is<Guid>(id => id == branchId),
            Arg.Is<string>(name => name == CashVarianceProductResolver.CashVarianceProductName));
    }
}
