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

    public ITransactionTypesRepository Build()
    {
        return _repository;
    }
}
