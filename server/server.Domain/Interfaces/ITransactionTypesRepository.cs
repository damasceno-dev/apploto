using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITransactionTypesRepository
{
    Task AddRange(IEnumerable<TransactionType> transactionTypes);
}
