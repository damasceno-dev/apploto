using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class ProductsRepositoryBuilder
{
    private readonly IProductsRepository _repository = Substitute.For<IProductsRepository>();

    public ProductsRepositoryBuilder ListActiveByIdsAndBranchIdAsNoTrackingReturns(
        IEnumerable<Guid> productIds,
        Guid branchId,
        IReadOnlyList<Product> result)
    {
        var idList = productIds.ToList();
        _repository.ListActiveByIdsAndBranchIdAsNoTracking(
                Arg.Is<IEnumerable<Guid>>(ids => ids.SequenceEqual(idList)),
                Arg.Is<Guid>(id => id == branchId))
            .Returns(result);
        return this;
    }

    public ProductsRepositoryBuilder GetActiveByBranchIdAndNameAsNoTrackingReturns(
        Guid branchId,
        string name,
        Product? result)
    {
        _repository.GetActiveByBranchIdAndNameAsNoTracking(
                Arg.Is<Guid>(id => id == branchId),
                Arg.Is<string>(n => n == name))
            .Returns(result);
        return this;
    }

    public ProductsRepositoryBuilder GetActiveByIdAndBranchId(Guid id, Guid branchId, Product? result)
    {
        _repository.GetActiveByIdAndBranchId(id, branchId).Returns(result);
        return this;
    }

    public ProductsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, Product? result)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(id, branchId).Returns(result);
        return this;
    }

    public ProductsRepositoryBuilder ListActiveByBranchIdAsNoTracking(Guid branchId, IReadOnlyList<Product> result)
    {
        _repository.ListActiveByBranchIdAsNoTracking(branchId).Returns(result);
        return this;
    }

    public ProductsRepositoryBuilder ExistsActiveByBranchIdAndName(Guid branchId, string name, bool result, Guid? exceptId = null)
    {
        _repository.ExistsActiveByBranchIdAndName(branchId, name, exceptId).Returns(result);
        return this;
    }

    public IProductsRepository Build()
    {
        return _repository;
    }
}
