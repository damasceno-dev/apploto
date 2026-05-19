using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class CategoriesRepositoryBuilder
{
    private readonly ICategoriesRepository _repository = Substitute.For<ICategoriesRepository>();

    public CategoriesRepositoryBuilder GetActiveByIdAndBranchId(Guid id, Guid branchId, Category? category)
    {
        _repository.GetActiveByIdAndBranchId(id, branchId).Returns(category);
        return this;
    }

    public CategoriesRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, Category? category)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(id, branchId).Returns(category);
        return this;
    }

    public CategoriesRepositoryBuilder GetActiveByIdAndBranchIdWithActiveTransactionTypes(Guid id, Guid branchId, Category? category)
    {
        _repository.GetActiveByIdAndBranchIdWithActiveTransactionTypes(id, branchId).Returns(category);
        return this;
    }

    public CategoriesRepositoryBuilder ListActiveByBranchIdAsNoTracking(Guid branchId, IReadOnlyList<Category> categories)
    {
        _repository.ListActiveByBranchIdAsNoTracking(branchId).Returns(categories);
        return this;
    }

    public CategoriesRepositoryBuilder ExistsActiveByBranchIdAndName(Guid branchId, string name, bool exists, Guid? exceptCategoryId = null)
    {
        _repository.ExistsActiveByBranchIdAndName(branchId, name, exceptCategoryId).Returns(exists);
        return this;
    }

    public ICategoriesRepository Build()
    {
        return _repository;
    }
}
