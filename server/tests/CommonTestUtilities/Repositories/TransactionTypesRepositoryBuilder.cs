using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class TransactionTypesRepositoryBuilder
{
    private readonly ITransactionTypesRepository _repository = Substitute.For<ITransactionTypesRepository>();

    public TransactionTypesRepositoryBuilder GetActiveByIdAndBranchIdWithCategoryAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        TransactionType? result)
    {
        _repository.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TransactionTypesRepositoryBuilder GetActiveByIdWithCategoryAndBranchId(
        Guid id, Guid branchId, TransactionType? result)
    {
        _repository.GetActiveByIdWithCategoryAndBranchId(
                Arg.Is<Guid>(v => v == id),
                Arg.Is<Guid>(v => v == branchId))
            .Returns(result);
        return this;
    }

    public TransactionTypesRepositoryBuilder GetActiveByIdWithCategoryAndBranchIdAsNoTracking(
        Guid id, Guid branchId, TransactionType? result)
    {
        _repository.GetActiveByIdWithCategoryAndBranchIdAsNoTracking(
                Arg.Is<Guid>(v => v == id),
                Arg.Is<Guid>(v => v == branchId))
            .Returns(result);
        return this;
    }

    public TransactionTypesRepositoryBuilder ListActiveByBranchIdAsNoTracking(
        Guid branchId, IReadOnlyList<TransactionType> result)
    {
        _repository.ListActiveByBranchIdAsNoTracking(
                Arg.Is<Guid>(v => v == branchId))
            .Returns(result);
        return this;
    }

    public TransactionTypesRepositoryBuilder ExistsActiveByCategoryIdAndName(
        Guid categoryId, string name, bool exists, Guid? exceptId = null)
    {
        _repository.ExistsActiveByCategoryIdAndName(
                Arg.Is<Guid>(v => v == categoryId),
                Arg.Is<string>(v => v == name),
                exceptId)
            .Returns(exists);
        return this;
    }

    public ITransactionTypesRepository Build()
    {
        return _repository;
    }
}
