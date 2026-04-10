using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class TransactionTypesRepositoryBuilder
{
    private readonly ITransactionTypesRepository _repository = Substitute.For<ITransactionTypesRepository>();

    public ITransactionTypesRepository Build()
    {
        return _repository;
    }
}
